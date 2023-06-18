using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Serialized;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.Platforms.AsmResolver;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace BaddyVM.VM;
internal class VMCore
{
	internal AssemblyDefinition assembly;
	internal ModuleDefinition module;

	internal VMContext context;

	internal VMCore(string path)
	{
		assembly = AssemblyDefinition.FromFile(path);
		module = assembly.ManifestModule;
		context = new VMContext(this);
		context.Init();
		//foreach(var t in module.GetAllTypes()) 
		//	t.IsSequentialLayout = true;
	}

	internal void Virtualize(IEnumerable<MethodDefinition> targets)
	{
		var methods = targets.Where(m => m.DeclaringType != context.VMType).ToArray(); // m.DeclaringType != context.VMType
		StringBuilder buffer = new(1024*4);
		Reloaded.Assembler.Assembler assembler = new();
		foreach(var method in methods) 
		{
			var architecture = new CilArchitecture(method.CilMethodBody);
			var resolver = new CilStateTransitioner(architecture);
			var controlflow = new SymbolicFlowGraphBuilder<CilInstruction>(architecture, method.CilMethodBody.Instructions, resolver).ConstructFlowGraph(0, Array.Empty<long>());
			var dataflow = new DataFlowParsed(resolver.DataFlowGraph, method.CilMethodBody);
			var blocks = BlockBuilder.ConstructBlocks(controlflow);
			var exc = method.CilMethodBody.ExceptionHandlers;
			var writer = new VMWriter() { buffer = buffer, assembler = assembler, ctx = context };
			var map = new LocalHeapMap(context);
			map.Register(method);

			writer.Header();

			foreach(var block in blocks.GetAllBlocks())
				ProcessBlock(exc, block, dataflow, method, writer, map);

			if (exc.Count > 0)
			{
				foreach(var tryblock in exc) // https://github.com/Washi1337/Echo/issues/12 :(
				{
					if (tryblock.HandlerStart != null && tryblock.HandlerType == CilExceptionHandlerType.Finally)
					{
						var instrrange = method.CilMethodBody.Instructions
							.SkipWhile(i => i.Offset != tryblock.HandlerStart.Offset)
							.TakeWhile(i => i.Offset <= tryblock.HandlerEnd.Offset);
						controlflow = new SymbolicFlowGraphBuilder<CilInstruction>(architecture, instrrange, resolver).ConstructFlowGraph(instrrange.First().Offset, Array.Empty<long>());
						blocks = BlockBuilder.ConstructBlocks(controlflow);
						//dataflow = new DataFlowParsed(resolver.DataFlowGraph, method.CilMethodBody);
						foreach (var block in blocks.GetAllBlocks())
							ProcessBlock(exc, block, dataflow, method, writer, map);
					}
				}
			}
			
			context.Virtualize(method.CilMethodBody, writer.Finish(), map);
			buffer.Clear();
		}
		assembler.Dispose();
	}

	private void ProcessBlock(IList<CilExceptionHandler> handlers, BasicBlock<CilInstruction> block, DataFlowParsed dfp, MethodDefinition md, VMWriter w, LocalHeapMap map)
	{
		var list = block.Instructions;
		w.Mark(list[0].Offset);
		VMTypes t1 = default, t2 = default;
		for(int i = 0; i < list.Count; i++)
		{
			var current = list[i];
			var trycatch = handlers.FirstOrDefault(h => h.TryStart.Offset == current.Offset || h.HandlerStart.Offset == current.Offset);
			if (trycatch != null)
			{
				var isfin = trycatch.HandlerType == CilExceptionHandlerType.Finally;
				var istry = trycatch.TryStart.Offset == current.Offset;
				if (istry)
				{
					if (isfin)
					{
						w.BeginTry(0);
					}
				}
				else
				{
					if(isfin)
					{
						w.BeginFinally();
					}
				}
			}

			switch (current.OpCode.Code)
			{
				case CilCode.Nop: break;
				case CilCode.Dup: w.Code(VMCodes.Dup); break;
				case CilCode.Pop: w.Code(VMCodes.Pop); break;

				#region jumps
				case CilCode.Br_S:
				case CilCode.Br: w.Br(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Brtrue_S:
				case CilCode.Brtrue: w.Brtrue(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Brfalse_S:
				case CilCode.Brfalse: w.Brfalse(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Beq_S:
				case CilCode.Beq: w.Beq(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Bgt_S:
				case CilCode.Bgt: w.Bgt(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Bgt_Un_S:
				case CilCode.Bgt_Un: w.BgtUn(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Ble_S:
				case CilCode.Ble: w.Ble(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Ble_Un_S:
				case CilCode.Ble_Un: w.BleUn(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Bge_S:
				case CilCode.Bge: w.Bge(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Bge_Un_S:
				case CilCode.Bge_Un: w.BgeUn(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Blt_S:
				case CilCode.Blt: w.Blt(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Blt_Un_S:
				case CilCode.Blt_Un: w.BltUn(((CilInstructionLabel)current.Operand).Offset); break;
				case CilCode.Bne_Un_S:
				case CilCode.Bne_Un: w.Bne(((CilInstructionLabel)current.Operand).Offset); break;
				#endregion
				#region number
				case CilCode.Ldc_I8: w.LoadNumber((long)current.Operand); break;
				case CilCode.Ldc_R4:
					{
						var res = (float)current.Operand;
						w.LoadNumber(Unsafe.As<float, int>(ref res));
						break;
					}
				case CilCode.Ldc_R8:
					{
						var d = (double)current.Operand;
						w.LoadNumber(Unsafe.As<double, long>(ref d)); break;
					}
				case CilCode.Ldc_I4:
				case CilCode.Ldc_I4_S:
				case CilCode.Ldc_I4_M1:
				case CilCode.Ldc_I4_0:
				case CilCode.Ldc_I4_1:
				case CilCode.Ldc_I4_2:
				case CilCode.Ldc_I4_3:
				case CilCode.Ldc_I4_4:
				case CilCode.Ldc_I4_5:
				case CilCode.Ldc_I4_6:
				case CilCode.Ldc_I4_7:
				case CilCode.Ldc_I4_8: w.LoadNumber(current.GetLdcI4Constant()); break;
				#endregion
				#region math
				case CilCode.Add: w.Add(dfp.ResolveIn1(current).IsFloat()); break;
				case CilCode.Add_Ovf: w.Add_Ovf(); break;
				case CilCode.Add_Ovf_Un: w.Add_Ovf_Un(); break;
				case CilCode.Sub: w.Sub(dfp.ResolveIn1(current).IsFloat()); break;
				case CilCode.Sub_Ovf: w.Sub_Ovf(); break;
				case CilCode.Sub_Ovf_Un: w.Sub_Ovf_Un(); break;
				case CilCode.Rem: w.Rem(dfp.ResolveIn1(current).IsFloat()); break;
				case CilCode.Rem_Un: w.Rem_Un(); break;
				case CilCode.Mul: dfp.ResolveIn2(current, ref t1, ref t2); w.Mul(t1, t2, t1.IsFloat()); break;
				case CilCode.Mul_Ovf: dfp.ResolveIn2(current, ref t1, ref t2); w.Mul_Ovf(t1, t2); break;
				case CilCode.Mul_Ovf_Un: w.Mul_Ovf_Un(); break;
				case CilCode.Div: dfp.ResolveIn2(current, ref t1, ref t2); w.Div(t1, t2, t1.IsFloat()); break;
				case CilCode.Div_Un: w.Div_Un(); break;
				#endregion
				#region logic
				case CilCode.Xor: w.Xor(); break;
				case CilCode.Or: w.Or(); break;
				case CilCode.And: w.And(); break;
				case CilCode.Not: w.Not(); break;
				case CilCode.Neg: w.Neg(); break;
				case CilCode.Shl: w.Shl(); break;
				case CilCode.Shr: w.Shr(); break;
				case CilCode.Shr_Un: w.Shr_Un(); break;
				case CilCode.Ceq: w.Code(VMCodes.Ceq); break;
				case CilCode.Cgt: w.Code(VMCodes.Cgt); break;
				case CilCode.Cgt_Un: w.Code(VMCodes.Cgt_Un); break;
				case CilCode.Clt: w.Code(VMCodes.Clt); break;
				case CilCode.Clt_Un: w.Code(VMCodes.Clt_Un); break;
				#endregion
				#region args & locals
				case CilCode.Ldarg_0:
				case CilCode.Ldarg_1:
				case CilCode.Ldarg_2:
				case CilCode.Ldarg_3:
				case CilCode.Ldarg_S:
				case CilCode.Ldarg:
					{
						var p = current.GetParameter(md.Parameters);
						var isStruct = p.ParameterType.IsStruct();
						if (p.Index == -1)
							isStruct = false;
						w.LoadLocal(map.Get(p), isStruct); break;
					}

				case CilCode.Ldarga:
				case CilCode.Ldarga_S: w.LoadLocalRef(map.Get(current.GetLocalVariable(md.CilMethodBody.LocalVariables))); break;

				case CilCode.Starg_S:
				case CilCode.Starg:
					{
						var param = current.GetParameter(md.Parameters);
						w.StoreLocal(map.Get(param), param.ParameterType.IsStruct(), (ushort)param.ParameterType.GetImpliedMemoryLayout(false).Size); break;
					}

				case CilCode.Ldloc:
				case CilCode.Ldloc_S:
				case CilCode.Ldloc_0:
				case CilCode.Ldloc_1:
				case CilCode.Ldloc_2:
				case CilCode.Ldloc_3:
					{
						var l = current.GetLocalVariable(md.CilMethodBody.LocalVariables);
						w.LoadLocal(map.Get(l), l.VariableType.IsStruct()); break;
					}

				case CilCode.Ldloca:
				case CilCode.Ldloca_S: w.LoadLocalRef(map.Get(current.GetLocalVariable(md.CilMethodBody.LocalVariables))); break;

				case CilCode.Stloc_0:
				case CilCode.Stloc_1:
				case CilCode.Stloc_2:
				case CilCode.Stloc_3:
				case CilCode.Stloc_S:
				case CilCode.Stloc:
					{
						var local = current.GetLocalVariable(md.CilMethodBody.LocalVariables);
						w.StoreLocal(map.Get(local), local.VariableType.IsStruct(), (ushort)local.VariableType.GetImpliedMemoryLayout(false).Size); 
						break;
					}
				#endregion
				#region fields
				case CilCode.Ldfld:
					{
						var op = ((IFieldDescriptor)current.Operand).Resolve();
						//var offset = context.GetOffset(op.DeclaringType, op);
						var size = op.Signature.FieldType.GetImpliedMemoryLayout(false).Size;
						w.LoadField(context.TransformFieldOffset(op), size); 
						break;
					}
				case CilCode.Ldflda:
					{
						var op = ((IFieldDescriptor)current.Operand).Resolve();
						//var offset = context.GetOffset(op.DeclaringType, op);
						w.LoadFieldRef(context.TransformFieldOffset(op));
						break;
					}
				case CilCode.Stfld:
					{
						var op = ((IFieldDescriptor)current.Operand).Resolve();
						//var offset = context.GetOffset(op.DeclaringType, op);
						var size = op.Signature.FieldType.GetImpliedMemoryLayout(false).Size;
						w.SetField(context.TransformFieldOffset(op), size);
						break;
					}
				#endregion

				case CilCode.Ldnull: w.LoadNumber(0); break;

				case CilCode.Ldsfld: w.LoadStaticField(context.Transform((MetadataMember)current.Operand)); break;
				case CilCode.Ldsflda: w.LoadStaticFieldRef(context.Transform((MetadataMember)current.Operand)); break;
				case CilCode.Stsfld: w.SetStaticField(context.Transform((MetadataMember)current.Operand)); break;

				case CilCode.Ldftn: w.LoadVMTable(context.Transform((MetadataMember)current.Operand)); break;

				case CilCode.Calli: w.Calli((byte)((IMethodDescriptor)current.Operand).Signature.GetTotalParameterCount(),
					((IMethodDescriptor)current.Operand).Signature.ReturnsValue); break;
				case CilCode.Call:
					{
						var method = (IMethodDescriptor)current.Operand;
						var isgeneric = current.Operand is SerializedMemberReference smr;
						if (!isgeneric) smr = null;
						else smr = (SerializedMemberReference)current.Operand; // meh, csharp

						var sig = isgeneric ? (MethodSignature)smr.Signature : method.Signature;
						var idx = context.Transform((MetadataMember)current.Operand);
						var pcount = (byte)sig.GetTotalParameterCount();
						var rets = method.Signature.ReturnsValue;
						if (!method.Signature.ReturnType.IsStruct())
							w.Call(idx, pcount, rets);
						else
							w.SafeCall(idx);
						break;
					}

				case CilCode.Callvirt:
					{
						var func = ((IMethodDefOrRef)current.Operand).Resolve();
						if (func.IsVirtual == false) // why clr, why you do this for non-virt methods???
							goto case CilCode.Call;

						if (func.DeclaringType.IsDelegate()) // say no to pain
							goto case CilCode.Call;

						if (func.DeclaringType.IsInterface)
						{
							if (list[i-1].OpCode == CilOpCodes.Constrained)
							{
								//w.CallInterface(context.TransformCallInterface(func), true);
								//w.Code(VMCodes.Pop);
								var comparer = new SignatureComparer();
								MetadataMember resolvedFunc; 
								var type = ((ITypeDefOrRef)list[i - 1].Operand);
								if (type is TypeDefinition td)
									resolvedFunc = td.Methods.First(m => m.Name == func.Name && comparer.Equals(m.Signature, func.Signature));
								else
								{
									//var resolvedType = type.Resolve();
									//resolvedFunc = (MetadataMember)module.DefaultImporter.ImportMethod(resolvedType.Methods.First(m => m.Name == func.Name && comparer.Equals(m.Signature, func.Signature)));
									resolvedFunc = type.CreateMemberReference(func.Name, func.Signature);
								}
								w.Call(context.Transform(resolvedFunc), (byte)func.Signature.GetTotalParameterCount(), func.Signature.ReturnsValue);
							}
							else
								w.CallInterface(context.TransformCallInterface(func), false);
						}
						else
							w.CallVirt(VirtualDispatcher.GetOffset(func), (byte)func.Signature.GetTotalParameterCount(), func.Signature.ReturnsValue);
						break;
					}

				case CilCode.Newobj:
					{
						if (((IMethodDefOrRef)current.Operand).DeclaringType.Resolve().IsByRefLike)
						{
							var ctor = (IMethodDefOrRef)current.Operand;
							var parent = (MetadataMember)ctor.DeclaringType;
							var args = (byte)ctor.Signature.GetTotalParameterCount();
							if (list[i+1].IsStloc() && list[i+1].GetLocalVariable(md.CilMethodBody.LocalVariables).VariableType.Resolve().IsByRefLike)
							{
								i++;
								w.LoadLocalRef(map.Get(list[i].GetLocalVariable(md.CilMethodBody.LocalVariables)));
								w.PushBack((ushort)(args-1));
								w.Call(context.Transform((MetadataMember)ctor), args, false);
								break;
							}
							throw new NotSupportedException("newobj byreflike type");
						}
						//else if (current.Operand is SerializedMemberReference smr)
						//{
						//	var ctor = smr;
						//	var parent = (MetadataMember)ctor.DeclaringType;
						//	w.NewObj(context.Transform(parent), context.Transform(ctor), (byte)((MethodSignature)ctor.Signature).GetTotalParameterCount());
						//}
						else
						{
							var ctor = (IMethodDefOrRef)current.Operand;
							if (ctor.DeclaringType.IsDelegate())
							{
								w.CreAAAAAAAAAAAAteDelegAAAAAAAAAAAAAte(context.Transform((MetadataMember)ctor.DeclaringType),
									((IMethodDefOrRef)list[i-1].Operand).Signature.HasThis == false);
								break;
							}
							var parent = (MetadataMember)ctor.DeclaringType;
							w.NewObj(context.Transform(parent),
								context.Transform((MetadataMember)ctor),
								(byte)((MethodSignature)ctor.Signature).GetTotalParameterCount());
						}
						break;
					}

				case CilCode.Ldstr: w.Ldstr((string)current.Operand); break;

				case CilCode.Ldtoken: w.LoadVMTable(context.TransformLdtoken((MetadataMember)current.Operand)); break;

				case CilCode.Leave:
				case CilCode.Leave_S: w.Leave(((CilInstructionLabel)current.Operand).Offset); break;

				case CilCode.Endfinally: w.EndFinally(); break;

				#region arrays
				case CilCode.Newarr: w.NewArr(context.Transform((MetadataMember)current.Operand)); break;
				case CilCode.Ldlen: w.Ldlen(); break;
				case CilCode.Stelem_I8:
				case CilCode.Stelem_R8:
				case CilCode.Stelem_Ref:
				case CilCode.Stelem_I: w.StelemI(); break;
				case CilCode.Stelem_I1: w.StelemI1(); break;
				case CilCode.Stelem_I2: w.StelemI2(); break;
				case CilCode.Stelem_R4:
				case CilCode.Stelem_I4: w.StelemI4(); break;
				case CilCode.Ldelem_I8:
				case CilCode.Ldelem_R8:
				case CilCode.Ldelem_Ref:
				case CilCode.Ldelem_I: w.LdelemI(); break;
				case CilCode.Ldelem_U1:
				case CilCode.Ldelem_I1: w.LdelemI1(); break;
				case CilCode.Ldelem_U2:
				case CilCode.Ldelem_I2: w.LdelemI2(); break;
				case CilCode.Ldelem_U4:
				case CilCode.Ldelem_R4:
				case CilCode.Ldelem_I4: w.LdelemI4(); break;
				#endregion

				#region set/deref pointer
				case CilCode.Ldind_Ref:
				case CilCode.Ldind_I: w.DerefI(); break;
				case CilCode.Ldind_U1:
				case CilCode.Ldind_I1: w.DerefI1(); break;
				case CilCode.Ldind_U2:
				case CilCode.Ldind_I2: w.DerefI2(); break;
				case CilCode.Ldind_R4:
				case CilCode.Ldind_U4:
				case CilCode.Ldind_I4: w.DerefI4(); break;
				case CilCode.Ldind_R8:
				case CilCode.Ldind_I8: w.DerefI8(); break;

				case CilCode.Stind_I1: w.SetI1(); break;
				case CilCode.Stind_I2: w.SetI2(); break;
				case CilCode.Stind_R4:
				case CilCode.Stind_I4: w.SetI4(); break;
				case CilCode.Stind_R8:
				case CilCode.Stind_I:
				case CilCode.Stind_Ref:
				case CilCode.Stind_I8: w.SetI(); break;
				#endregion
				#region Converters
				case CilCode.Conv_U:
				case CilCode.Conv_I:
				case CilCode.Conv_I8:
				case CilCode.Conv_I4:
				case CilCode.Conv_I2:
				case CilCode.Conv_I1:
				case CilCode.Conv_U8:
				case CilCode.Conv_U4:
				case CilCode.Conv_U2:
				case CilCode.Conv_U1:
				case CilCode.Conv_Ovf_U:
				case CilCode.Conv_Ovf_I:
				case CilCode.Conv_Ovf_I8:
				case CilCode.Conv_Ovf_I4:
				case CilCode.Conv_Ovf_I2:
				case CilCode.Conv_Ovf_I1:
				case CilCode.Conv_Ovf_U8:
				case CilCode.Conv_Ovf_U4:
				case CilCode.Conv_Ovf_U2:
				case CilCode.Conv_Ovf_U1:
				case CilCode.Conv_Ovf_U_Un:
				case CilCode.Conv_Ovf_I_Un:
				case CilCode.Conv_Ovf_I8_Un:
				case CilCode.Conv_Ovf_I4_Un:
				case CilCode.Conv_Ovf_I2_Un:
				case CilCode.Conv_Ovf_I1_Un:
				case CilCode.Conv_Ovf_U8_Un:
				case CilCode.Conv_Ovf_U4_Un:
				case CilCode.Conv_Ovf_U2_Un:
				case CilCode.Conv_Ovf_U1_Un:
				case CilCode.Conv_R_Un:
				case CilCode.Conv_R4:
				case CilCode.Conv_R8: w.Conv(dfp.ResolveIn1(current), current.OpCode.Code); break;
				#endregion

				case CilCode.Sizeof: w.LoadNumber(((ITypeDefOrRef)current.Operand).GetImpliedMemoryLayout(false).Size); break;

				case CilCode.Ret: w.Ret(); break;

				case CilCode.Constrained:
					{
						var type = current.Operand as ITypeDefOrRef;
						var isStruct = type.ToTypeSignature().IsStruct();
						if (list[i+1].OpCode.Code == CilCode.Callvirt && isStruct)
						{
							//var method = (IMethodDefOrRef)list[i+1].Operand;
							//w.LoadVMTable(context.TransformLdtoken((MetadataMember)type));
						}
						break;
					}

				case CilCode.Initobj:
					{
						var type = current.Operand as ITypeDefOrRef;
						if (!type.ToTypeSignature().IsStruct())
							throw new NotImplementedException("case Initobj -> type != struct");

						w.Initobj(type.GetImpliedMemoryLayout(false).Size);
						break;
					}

				case CilCode.Initblk: w.Code(VMCodes.Initblk); break;

				case CilCode.Break:
				case CilCode.Jmp:
				case CilCode.Switch:
				case CilCode.Cpobj:
				case CilCode.Ldobj:
				case CilCode.Throw:
				case CilCode.Stobj:
				case CilCode.Box: // req own mem allocator or gc hooks or BIG switch for all box moments
				case CilCode.Unbox_Any: // same
				case CilCode.Unbox: // same
				case CilCode.Isinst: // same ?
				case CilCode.Castclass: // same?
				case CilCode.Ldelema:
				case CilCode.Ldelem:
				case CilCode.Stelem:
				case CilCode.Refanyval:
				case CilCode.Ckfinite:
				case CilCode.Mkrefany:
				case CilCode.Prefix7:
				case CilCode.Prefix6:
				case CilCode.Prefix5:
				case CilCode.Prefix4:
				case CilCode.Prefix3:
				case CilCode.Prefix2:
				case CilCode.Prefix1:
				case CilCode.Prefixref:
				case CilCode.Arglist:
				case CilCode.Ldvirtftn:
				case CilCode.Localloc:
				case CilCode.Endfilter:
				case CilCode.Unaligned:
				case CilCode.Volatile:
				case CilCode.Tailcall:
				case CilCode.Cpblk:
				case CilCode.Rethrow:
				case CilCode.Refanytype:
				case CilCode.Readonly:
				default: throw new NotImplementedException(current.OpCode.ToString());
			}
		}
	}

	internal void Save(string path)
	{
		context.Inject();
		module.IsILOnly = false;
		module.PEKind = AsmResolver.PE.File.Headers.OptionalHeaderMagic.PE32Plus;
		module.MachineType = AsmResolver.PE.File.Headers.MachineType.Amd64;
		module.IsBit32Preferred = false;
		module.IsBit32Required = false;
		
		/* var image = module.ToPEImage();
		var filebuilfer = new ManagedPEFileBuilder();
		var file = filebuilfer.CreateFile(image);
		var builder = new SegmentBuilder();
		foreach(var i in context.FixOffsets.Values)
		{
			builder.Add(i);
		}
		file.Sections.Add(new AsmResolver.PE.File.PESection(".tеxt", AsmResolver.PE.File.Headers.SectionFlags.MemoryRead | AsmResolver.PE.File.Headers.SectionFlags.MemoryWrite, builder));
		file.Write(path);
		*/

		assembly.Write(path);
	}
}
