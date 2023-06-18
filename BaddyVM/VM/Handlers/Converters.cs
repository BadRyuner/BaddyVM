using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using System;

namespace BaddyVM.VM.Handlers;
internal class Converters
{
	internal static void Handle(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Converter").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var buf);
		i.NewLocal(ctx, out var inType);
		i.NewLocal(ctx, out var convType);
		i.DecodeCode(1).Save(inType);
		i.DecodeCode(2).Save(convType);
		var typeToLocal = new Dictionary<VMTypes, CilLocalVariable>() 
		{
			{ VMTypes.I1, i.NewLocal(ctx, VMTypes.I1) },
			{ VMTypes.U1, i.NewLocal(ctx, VMTypes.U1) },
			{ VMTypes.I2, i.NewLocal(ctx, VMTypes.I2) },
			{ VMTypes.U2, i.NewLocal(ctx, VMTypes.U2) },
			{ VMTypes.I4, i.NewLocal(ctx, VMTypes.I4) },
			{ VMTypes.U4, i.NewLocal(ctx, VMTypes.U4) },
			{ VMTypes.I8, i.NewLocal(ctx, VMTypes.I8) },
			{ VMTypes.U8, i.NewLocal(ctx, VMTypes.U8) },
			{ VMTypes.R4, i.NewLocal(ctx, VMTypes.R4) },
			{ VMTypes.R8, i.NewLocal(ctx, VMTypes.R8) },
			{ VMTypes.PTR, i.NewLocal(ctx, VMTypes.PTR) },
		};

		var toEnd = new CilInstruction(CilOpCodes.Nop);
		var instr = new CilOpCode[] { CilOpCodes.Conv_I, CilOpCodes.Conv_U, CilOpCodes.Conv_I1, CilOpCodes.Conv_U1, CilOpCodes.Conv_I2, CilOpCodes.Conv_U2, CilOpCodes.Conv_I4, CilOpCodes.Conv_U4, CilOpCodes.Conv_I8, CilOpCodes.Conv_U8, CilOpCodes.Conv_R4, CilOpCodes.Conv_R8, CilOpCodes.Conv_Ovf_I, CilOpCodes.Conv_Ovf_U, CilOpCodes.Conv_Ovf_I1, CilOpCodes.Conv_Ovf_U1, CilOpCodes.Conv_Ovf_I2, CilOpCodes.Conv_Ovf_U2, CilOpCodes.Conv_Ovf_I4, CilOpCodes.Conv_Ovf_U4, CilOpCodes.Conv_Ovf_I8, CilOpCodes.Conv_Ovf_U8, CilOpCodes.Conv_Ovf_I_Un, CilOpCodes.Conv_Ovf_U_Un, CilOpCodes.Conv_Ovf_I1_Un, CilOpCodes.Conv_Ovf_U1_Un, CilOpCodes.Conv_Ovf_I2_Un, CilOpCodes.Conv_Ovf_U2_Un, CilOpCodes.Conv_Ovf_I4_Un, CilOpCodes.Conv_Ovf_U4_Un, CilOpCodes.Conv_Ovf_I8_Un, CilOpCodes.Conv_Ovf_U8_Un, CilOpCodes.Conv_R_Un };

		foreach(var ins in instr)
			i.LoadNumber((ushort)ins.Code).Load(convType).Compare().IfTrue(() =>
			{
				foreach (var type in typeToLocal)
					i.Load(inType).LoadNumber((byte)type.Key).Compare().IfTrue(() =>
					{
						i.PopMem(ctx, buf).Save(type.Value).Load(type.Value);
						i.Add(ins);
						i.Save(inType).PushMem(ctx, inType, buf);
						i.Br(toEnd.CreateLabel());
					});
			});

		i.Add(toEnd);
		i.RegisterHandler(ctx, VMCodes.Conv);
	}
}
