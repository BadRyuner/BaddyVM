using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Serialized;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using Echo.DataFlow;
using Echo.DataFlow.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BaddyVM.VM.Utils;
internal class DataFlowParsed
{
	Dictionary<int, (VMTypes[] @in, VMTypes @out)> parsed;
	CilMethodBody body;

	internal DataFlowParsed(DataFlowGraph<CilInstruction> graph, CilMethodBody body)
	{
		parsed = new(graph.Nodes.Count);
		this.body = body;
		foreach(var node in graph.Nodes)
			Parse(node);
		return; // for dbg point
	}

	internal VMTypes ResolveIn1(CilInstruction instr) => parsed[instr.Offset].@in[0];
	internal void ResolveIn2(CilInstruction instr, ref VMTypes one, ref VMTypes two)
	{
		one = parsed[instr.Offset].@in[0];
		two = parsed[instr.Offset].@in[1];
	}

	internal VMTypes[] ResolveAll(CilInstruction instr) => parsed[instr.Offset].@in;
	
	private void Parse(DataFlowNode<CilInstruction> node)
	{
		if (parsed.ContainsKey(node.Contents.Offset))
			return;

		var incoming = new List<DataFlowNode<CilInstruction>>();
		incoming.AddRange(node.StackDependencies.Select(s => s.First().Node));
		var incomingcount = incoming.Count();
		VMTypes[] _incoming = new VMTypes[incomingcount];
		int counter = 0;
		if (incomingcount != 0)
		{
			foreach (var i in incoming)
			{
				_incoming[counter] = ResolveDataSource(i);
				counter++;
			}
		}
		parsed.Add(node.Contents.Offset, (_incoming, GetResult(node.Contents, _incoming)));
	}

	private VMTypes ResolveDataSource(DataFlowNode<CilInstruction> source)
	{
		if (parsed.TryGetValue(source.Contents.Offset, out var result))
			return result.@out;
		if (source.GetOutgoingEdges().Count() == 0)
			return GetResult(source.Contents, null);

		Parse(source);
		return parsed[source.Contents.Offset].@out;
	}

	private VMTypes GetResult(CilInstruction instr, VMTypes[] incoming)
	{
		switch (instr.OpCode.Code)
		{
			case CilCode.Ldarg_0:
			case CilCode.Ldarg_1:
			case CilCode.Ldarg_2:
			case CilCode.Ldarg_3:
				return instr.GetParameter(body.Owner.Parameters).ParameterType.ElementType.ToVMTypes();

			case CilCode.Ldloc_0:
				return body.LocalVariables[0].VariableType.ElementType.ToVMTypes();

			case CilCode.Ldloc_1:
				return body.LocalVariables[1].VariableType.ElementType.ToVMTypes();

			case CilCode.Ldloc_2:
				return body.LocalVariables[2].VariableType.ElementType.ToVMTypes();

			case CilCode.Ldloc_3:
				return body.LocalVariables[3].VariableType.ElementType.ToVMTypes();

			case CilCode.Ldarg_S:
			case CilCode.Ldarg:
				return ((Parameter)instr.Operand).ParameterType.ElementType.ToVMTypes();

			case CilCode.Ldarga_S:
			case CilCode.Ldarga:
				return VMTypes.PTR_PTR;

			case CilCode.Ldloc_S:
			case CilCode.Ldloca_S:
			case CilCode.Ldloc:
			case CilCode.Ldloca:
				return ((CilLocalVariable)instr.Operand).VariableType.ElementType.ToVMTypes();

			case CilCode.Ldc_I4_M1:
			case CilCode.Ldc_I4_0:
			case CilCode.Ldc_I4_1:
			case CilCode.Ldc_I4_2:
			case CilCode.Ldc_I4_3:
			case CilCode.Ldc_I4_4:
			case CilCode.Ldc_I4_5:
			case CilCode.Ldc_I4_6:
			case CilCode.Ldc_I4_7:
			case CilCode.Ldc_I4_8:
			case CilCode.Ldc_I4_S:
			case CilCode.Ldc_I4:
			case CilCode.Ldind_I4:
			case CilCode.Conv_I4:
			case CilCode.Sizeof:
			case CilCode.Conv_Ovf_I4:
			case CilCode.Conv_Ovf_I4_Un:
			case CilCode.Ceq:
			case CilCode.Cgt:
			case CilCode.Cgt_Un:
			case CilCode.Clt:
			case CilCode.Clt_Un:
			case CilCode.Ldtoken:
			case CilCode.Ldlen:
			case CilCode.Ldelem_I4:
				return VMTypes.I4;

			case CilCode.Ldc_I8:
			case CilCode.Ldind_I8:
			case CilCode.Conv_I8:
			case CilCode.Ldnull:
			case CilCode.Conv_Ovf_I8:
			case CilCode.Conv_Ovf_I8_Un:
			case CilCode.Ldftn:
			case CilCode.Ldvirtftn:
			case CilCode.Ldelem_I8:
				return VMTypes.I8;

			case CilCode.Conv_Ovf_U8:
			case CilCode.Conv_Ovf_U8_Un:
			case CilCode.Conv_U8:
				return VMTypes.U8;

			case CilCode.Ldc_R4:
			case CilCode.Ldind_R4:
			case CilCode.Conv_R4:
			case CilCode.Ldelem_R4:
				return VMTypes.R4;

			case CilCode.Ldc_R8:
			case CilCode.Ldind_R8:
			case CilCode.Conv_R8:
			case CilCode.Conv_R_Un:
			case CilCode.Ldelem_R8:
				return VMTypes.R8;

			case CilCode.Dup:
			case CilCode.Neg:
			case CilCode.Not:
				return incoming[0];

			case CilCode.Calli:
				throw new NotImplementedException();

			case CilCode.Ckfinite:
			case CilCode.Ldind_I1:
			case CilCode.Conv_I1:
			case CilCode.Conv_Ovf_I1:
			case CilCode.Conv_Ovf_I1_Un:
			case CilCode.Isinst:
			case CilCode.Ldelem_I1:
				return VMTypes.I1;

			case CilCode.Ldind_U1:
			case CilCode.Conv_U1:
			case CilCode.Conv_Ovf_U1:
			case CilCode.Conv_Ovf_U1_Un:
			case CilCode.Ldelem_U1:
				return VMTypes.U1;

			case CilCode.Ldind_I2:
			case CilCode.Conv_I2:
			case CilCode.Conv_Ovf_I2:
			case CilCode.Conv_Ovf_I2_Un:
			case CilCode.Ldelem_I2:
				return VMTypes.I2;

			case CilCode.Ldind_U2:
			case CilCode.Conv_U2:
			case CilCode.Conv_Ovf_U2:
			case CilCode.Conv_Ovf_U2_Un:
			case CilCode.Ldelem_U2:
				return VMTypes.U2;

			case CilCode.Ldind_U4:
			case CilCode.Conv_U4:
			case CilCode.Conv_Ovf_U4:
			case CilCode.Conv_Ovf_U4_Un:
			case CilCode.Ldelem_U4:
				return VMTypes.U4;

			case CilCode.Ldind_I:
			case CilCode.Localloc:
			case CilCode.Conv_I:
			case CilCode.Conv_Ovf_I:
			case CilCode.Conv_Ovf_U:
			case CilCode.Conv_U:
			case CilCode.Conv_Ovf_I_Un:
			case CilCode.Conv_Ovf_U_Un:
			case CilCode.Ldelem_Ref:
			case CilCode.Ldelem_I:
				return VMTypes.PTR;

			case CilCode.Add:
			case CilCode.Sub:
			case CilCode.Mul:
			case CilCode.Div:
			case CilCode.Div_Un:
			case CilCode.Rem:
			case CilCode.Rem_Un:
			case CilCode.And:
			case CilCode.Or:
			case CilCode.Xor:
			case CilCode.Shl:
			case CilCode.Shr:
			case CilCode.Shr_Un:
			case CilCode.Add_Ovf:
			case CilCode.Add_Ovf_Un:
			case CilCode.Mul_Ovf:
			case CilCode.Mul_Ovf_Un:
			case CilCode.Sub_Ovf:
			case CilCode.Sub_Ovf_Un:
				return GeneralUtils.MathWith(incoming[0], incoming[1]);

			case CilCode.Ldstr:
				return VMTypes.STR;

			case CilCode.Newobj:
			case CilCode.Callvirt:
			case CilCode.Call:
				return VMTypes.PTR;

			case CilCode.Unbox:
				return VMTypes.PTR;

			case CilCode.Ldfld:
				return ((FieldDefinition)instr.Operand).Signature.FieldType.IsValueType ? VMTypes.PTR : VMTypes.PTR;

			case CilCode.Ldsflda:
			case CilCode.Ldflda:
				return ((FieldDefinition)instr.Operand).Signature.FieldType.IsValueType ? VMTypes.PTR : VMTypes.PTR_PTR;
				
			case CilCode.Box:
			case CilCode.Newarr:
			case CilCode.Castclass:
				return VMTypes.PTR;

			case CilCode.Ldelema:
				return VMTypes.PTR;
			case CilCode.Ldelem:
				return ((TypeSignature)instr.Operand).ElementType.ToVMTypes();

			case CilCode.Unbox_Any:
				return VMTypes.PTR;

			case CilCode.Prefix7:
			case CilCode.Prefix6:
			case CilCode.Prefix5:
			case CilCode.Prefix4:
			case CilCode.Prefix3:
			case CilCode.Prefix2:
			case CilCode.Prefix1:
			case CilCode.Prefixref:
			case CilCode.Arglist:
			case CilCode.Refanyval:
			case CilCode.Mkrefany:
			case CilCode.Cpobj:
			case CilCode.Ldobj:
				throw new NotImplementedException();

			case CilCode.Starg:
			case CilCode.Stloc:
			case CilCode.Initobj:
			case CilCode.Cpblk:
			case CilCode.Refanytype:
			case CilCode.Rethrow:
			case CilCode.Readonly:
			case CilCode.Unaligned:
			case CilCode.Constrained:
			case CilCode.Stind_I:
			case CilCode.Endfilter:
			case CilCode.Volatile:
			case CilCode.Throw:
			case CilCode.Tailcall:
			case CilCode.Initblk:
			case CilCode.Stfld:
			case CilCode.Ldsfld:
			case CilCode.Stelem_I:
			case CilCode.Stelem_I1:
			case CilCode.Stelem_I2:
			case CilCode.Stelem_I4:
			case CilCode.Stelem_I8:
			case CilCode.Stelem_R4:
			case CilCode.Stelem_R8:
			case CilCode.Stelem_Ref:
			case CilCode.Stelem:
			case CilCode.Endfinally:
			case CilCode.Leave:
			case CilCode.Leave_S:
			case CilCode.Stsfld:
			case CilCode.Nop:
			case CilCode.Break:
			case CilCode.Pop:
			case CilCode.Ret:
			case CilCode.Br_S:
			case CilCode.Brfalse_S:
			case CilCode.Brtrue_S:
			case CilCode.Beq_S:
			case CilCode.Bge_S:
			case CilCode.Bgt_S:
			case CilCode.Ble_S:
			case CilCode.Blt_S:
			case CilCode.Bne_Un_S:
			case CilCode.Bge_Un_S:
			case CilCode.Bgt_Un_S:
			case CilCode.Ble_Un_S:
			case CilCode.Blt_Un_S:
			case CilCode.Br:
			case CilCode.Brfalse:
			case CilCode.Brtrue:
			case CilCode.Beq:
			case CilCode.Bge:
			case CilCode.Bgt:
			case CilCode.Ble:
			case CilCode.Blt:
			case CilCode.Bne_Un:
			case CilCode.Bge_Un:
			case CilCode.Bgt_Un:
			case CilCode.Ble_Un:
			case CilCode.Blt_Un:
			case CilCode.Switch:
			case CilCode.Ldind_Ref:
			case CilCode.Stind_Ref:
			case CilCode.Stind_I1:
			case CilCode.Stind_I2:
			case CilCode.Stind_I4:
			case CilCode.Stind_I8:
			case CilCode.Stind_R4:
			case CilCode.Stind_R8:
			case CilCode.Stloc_0:
			case CilCode.Stloc_1:
			case CilCode.Stloc_2:
			case CilCode.Stloc_3:
			case CilCode.Starg_S:
			case CilCode.Stloc_S:
			case CilCode.Stobj:
			default:
				return VMTypes.None;
		}
	}
}
