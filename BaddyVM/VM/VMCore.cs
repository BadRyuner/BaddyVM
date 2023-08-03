using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Serialized;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using BaddyVM.VM.Protections;
using BaddyVM.VM.Protections.Transformations;
using BaddyVM.VM.Utils;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Construction.Symbolic;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.Platforms.AsmResolver;
using System.Runtime.CompilerServices;
using System.Text;

namespace BaddyVM.VM;
internal class VMCore
{
	internal ModuleDefinition ThisModule = ModuleDefinition.FromModule(typeof(VMCore).Assembly.ManifestModule);

	internal AssemblyDefinition assembly;
	internal ModuleDefinition module;
	internal bool ApplyProtections = false;
	internal string path;

	internal VMContext context;

	internal VMCore(string path, bool applyProtections)
	{
		this.path = path;
		ApplyProtections = applyProtections;
		assembly = AssemblyDefinition.FromFile(path);
		module = assembly.ManifestModule;
		context = new VMContext(this);
		context.Init();
	}

	internal void Virtualize(IEnumerable<MethodDefinition> targets)
	{
		var methods = targets.Where(m => m.DeclaringType != context.VMType && m.CilMethodBody?.Instructions.Any(i => i.OpCode.Code == CilCode.Jmp) == false).ToArray(); // m.DeclaringType != context.VMType
		StringBuilder buffer = new(1024*4);
		Reloaded.Assembler.Assembler assembler = new(1024*256, 1024*256);

		context.ProxyToCode = new(methods.Length);
		foreach(var method in methods)
			context.ProxyToCode.Add(method, context.AllocData(method.Name));

		foreach(var method in methods) 
		{
			if (method.DeclaringType == context.RefContainer) continue;
			ApplyIntrincisc(method);

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
				foreach(var tryblock in exc) // TODO: Replace with extension call
				{
					if (tryblock.HandlerStart != null && (tryblock.HandlerType == CilExceptionHandlerType.Finally || tryblock.HandlerType == CilExceptionHandlerType.Exception))
					{
						var instrrange = method.CilMethodBody.Instructions
							.SkipWhile(i => i.Offset != tryblock.HandlerStart.Offset);
							//.TakeWhile(i => i.Offset <= tryblock.HandlerEnd.Offset);
						controlflow = new SymbolicFlowGraphBuilder<CilInstruction>(architecture, instrrange, resolver).ConstructFlowGraph(instrrange.First().Offset, Array.Empty<long>());
						blocks = BlockBuilder.ConstructBlocks(controlflow);
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
		if (w.AlreadyMarkedOffsets.Contains(block.Instructions[0].Offset)) 
			return;
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
				var isExc = trycatch.HandlerType == CilExceptionHandlerType.Exception;
				var istry = trycatch.TryStart.Offset == current.Offset;
				if (istry) 
				{
					if (isfin) w.BeginFinTry(trycatch);
					else if (isExc) w.BeginTryCatch(trycatch, context.TransformTryCatch(trycatch.ExceptionType));
				}
				else 
				{
					if(isfin) w.BeginFinally(trycatch);
					else if (isExc) w.BeginTryCatchHandler(trycatch);
				}
			}

			switch (current.OpCode.Code)
			{
				case CilCode.Nop: break;
				case CilCode.Dup: w.Code(VMCodes.Dup); break;
				case CilCode.Pop: w.Code(VMCodes.Pop); break;

				#region jumps
				case CilCode.Switch: w.Switch(((List<ICilLabel>)current.Operand).Select(s => s.Offset).ToArray()); break;
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
						w.LoadLocal(map.Get(p), false); break;
					}

				case CilCode.Ldarga:
				case CilCode.Ldarga_S:
					{
						var p = current.GetParameter(md.Parameters);
						var isStruct = p.ParameterType.IsStruct();
						if (p.Index == -1)
							isStruct = false;
						if (isStruct)
							w.LoadLocal(map.Get(p), false); // yeaaaaaah, shitcode
						else
							w.LoadLocalRef(map.Get(p)); break;
					}

				case CilCode.Starg_S:
				case CilCode.Starg:
					{
						var param = current.GetParameter(md.Parameters);
						w.StoreLocal(map.Get(param), param.ParameterType.IsStruct(), 8 /* (ushort)param.ParameterType.GetImpliedMemoryLayout(false).Size */); break;
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
					((IMethodDescriptor)current.Operand).Signature.ReturnsValue, ((IMethodDescriptor)current.Operand).CalcFloatByte()); break;
				case CilCode.Call:
					{
						var method = (IMethodDescriptor)current.Operand;
						var replace = Intrinsics.IntrinsicsFactory.Get(method);
						if (replace != null)
							method = replace;
						var isgeneric = current.Operand is SerializedMemberReference smr;
						if (!isgeneric) smr = null;
						else smr = (SerializedMemberReference)current.Operand; // meh, csharp

						var sig = isgeneric ? (MethodSignature)smr.Signature : method.Signature;
						var idx = context.Transform((MetadataMember)method);
						var pcount = (byte)sig.GetTotalParameterCount();
						var rets = method.Signature.ReturnsValue;
						if (!method.Signature.ReturnType.IsStruct())
						{
							if (context.IsNet6() || sig.ParameterTypes.Any(t => t is ByReferenceTypeSignature || t is PointerTypeSignature) || sig.ReturnsValue) // idk how to handle ret val
							{
								w.Call(idx, pcount, rets, method.CalcFloatByte());
							}
							else
							{
								idx = context.TransformSignature((MetadataMember)method);
								Console.WriteLine($"Managed Call -> {method}");
								w.CallManaged(idx, sig, sig.ReturnsValue, sig.HasThis);
							}
						}
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
								w.Call(context.Transform(resolvedFunc), (byte)func.Signature.GetTotalParameterCount(), func.Signature.ReturnsValue, func.CalcFloatByte());
							}
							else
								w.CallInterface(context.TransformCallInterface(func), false);
						}
						else
							w.CallVirt(context.Transform(func), (byte)func.Signature.GetTotalParameterCount(), func.Signature.ReturnsValue, func.CalcFloatByte());
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
								w.Call(context.Transform((MetadataMember)ctor), args, false, ctor.CalcFloatByte());
								break;
							}
							throw new NotSupportedException("newobj byreflike type");
						}
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
							var size = ((ITypeDescriptor)parent).ToTypeSignature().IsStruct() ? 
								((ITypeDescriptor)parent).GetImpliedMemoryLayout(false).Size
								: 0;
							w.NewObj(context.Transform(parent),
								context.Transform((MetadataMember)ctor),
								(byte)((MethodSignature)ctor.Signature).GetTotalParameterCount(),
								((ITypeDescriptor)parent).IsValueType,
								size,
								(byte)(ctor.CalcFloatByte() << 1));
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

				case CilCode.Ret:
					{
						var retVal = md.Signature.ReturnType;
						var size = md.Signature.ReturnsValue ? (md.Signature.ReturnType.IsValueType ? md.Signature.ReturnType.GetImpliedMemoryLayout(false).Size : 8) : (uint)current.Offset;
						w.Ret((ushort)size); 
						break;
					}

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

				case CilCode.Box:
					{
						var type = (ITypeDefOrRef)current.Operand;
						w.Box(context.Transform((MetadataMember)type), (ushort)type.GetImpliedMemoryLayout(false).Size); break;
					}
				case CilCode.Unbox_Any:
				case CilCode.Unbox: w.Unbox((ushort)((ITypeDefOrRef)current.Operand).GetImpliedMemoryLayout(false).Size); break;

				case CilCode.Isinst:
					{
						var type = (ITypeDefOrRef)current.Operand;
						if (type.Resolve().IsInterface)
							w.IsinstInterface(context.Transform((MetadataMember)type));
						else
							w.Isinst(context.Transform((MetadataMember)type));
						break;
					}
				case CilCode.Castclass:
					{
						var type = (ITypeDefOrRef)current.Operand;
						if (type.Resolve().IsInterface)
							w.Castinterface(context.Transform((MetadataMember)type));
						else
							w.Castclass(context.Transform((MetadataMember)type));
						break;
					}

				case CilCode.Jmp:
					{
						var target = (IMethodDefOrRef)current.Operand;
						if (target is MethodDefinition dm && context.ProxyToCode.ContainsKey(dm))
						{
							w.Call(context.Transform(context.ProxyToCode[dm]), 0, true);
							w.Code(VMCodes.Jmp);
						}
						else // maybe replace with native method function and some assembler tricks?
						{
							foreach(var v in md.Parameters)
								w.LoadLocal(map.Get(v), v.ParameterType.IsStruct());
							w.Call(context.Transform((MetadataMember)target), (byte)target.Signature.GetTotalParameterCount(), target.Signature.ReturnsValue);
							var retVal = md.Signature.ReturnType;
							var size = md.Signature.ReturnsValue ? (md.Signature.ReturnType.IsValueType ? md.Signature.ReturnType.GetImpliedMemoryLayout(false).Size : 8) : (uint)current.Offset;
							w.Ret((ushort)size);
						}
						break;
					}

				case CilCode.Localloc:
					w.Call(context._Alloc(), 1, true);
					break;

				case CilCode.Throw:
					w.Code(VMCodes.Throw);
					break;

				// not tested
				case CilCode.Ldobj: break; // skip, all structs is byref in vm
				case CilCode.Stobj: w.SetSized((ushort)((IFieldDescriptor)current.Operand).Signature.FieldType.GetImpliedMemoryLayout(false).Size); break;

				case CilCode.Break:
				case CilCode.Cpobj:
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

	private void ApplyIntrincisc(MethodDefinition method)
	{
		var il = method.CilMethodBody.Instructions;
		for(int i = 0; i < il.Count - 1; i++)
		{
			var first = il[i];
			var second = il[i + 1];
			if (first.OpCode.Code == CilCode.Ldstr && second.OpCode.Code == CilCode.Call &&
				second.Operand is IMethodDescriptor imd && imd.Name == "op_Equality" && imd.DeclaringType.Name == "String")
			{
				// Only For someval == "somestring"
				second.Operand = SlowStringCompare.GetMethod(context, (string)first.Operand);
				first.ReplaceWithNop();
			}

			// TBD
		}
		il.CalculateOffsets();
	}

	internal void Save(string path)
	{
		context.Inject();
		module.IsILOnly = false;
		module.PEKind = AsmResolver.PE.File.Headers.OptionalHeaderMagic.PE32Plus;
		module.MachineType = AsmResolver.PE.File.Headers.MachineType.Amd64;
		module.IsBit32Preferred = false;
		module.IsBit32Required = false;

		if (ApplyProtections)
		{
			foreach (var type in module.GetAllTypes().Where(t => !t.IsModuleType))
			{
				GeneralProtections.ProtectType(context, type);
				foreach (var m in type.Methods.Where(m => m.CilMethodBody != null))
					GeneralProtections.Protect(context, m.CilMethodBody);
			}
		}

		var image = module.ToPEImage();

		if (ApplyProtections)
		{
			// TBD
		}
		
		var file = new ManagedPEFileBuilder().CreateFile(image);

		if (ApplyProtections)
		{
			// TBD
			var vmdata = new SegmentBuilder();

			NativeString.MovStrings(vmdata);
			vmdata.Add(context.FunctionsPointers);

			file.Sections.Add(new AsmResolver.PE.File.PESection(".aboba", AsmResolver.PE.File.Headers.SectionFlags.MemoryRead | AsmResolver.PE.File.Headers.SectionFlags.MemoryWrite, vmdata));
		}

		//assembly.Write(path);
		file.Write(path);
	}
}
