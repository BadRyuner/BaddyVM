using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using BaddyVM.VM.Utils;
using static Iced.Intel.AssemblerRegisters;

namespace BaddyVM.VM.Protections.General;
internal static class NativeOp
{
	private static MethodDefinition Ceq;
	private static MethodDefinition Add;
	private static MethodDefinition Sub;

	private static MethodDefinition LdindI;
	private static MethodDefinition LdindU1;
	private static MethodDefinition LdindU4;

	private static bool ready = false;

	internal static void Apply(VMContext ctx, CilMethodBody body)
	{
		if (!ready) Prepare(ctx);
		if (body.LocalVariables.Any(a => a.VariableType.ElementType == ElementType.R8)) return;
		for(int i = 0; i < body.Instructions.Count; i++)
		{
			var instr = body.Instructions[i];
			if (instr.OpCode.Code == CilCode.Ceq)
				instr.ReplaceWith(CilOpCodes.Call, Ceq);
			else if (instr.OpCode.Code == CilCode.Add)
				instr.ReplaceWith(CilOpCodes.Call, Add);
			else if (instr.OpCode.Code == CilCode.Sub)
				instr.ReplaceWith(CilOpCodes.Call, Sub);
			else if (instr.OpCode.Code == CilCode.Ldind_I || instr.OpCode.Code == CilCode.Ldind_I8)
				instr.ReplaceWith(CilOpCodes.Call, LdindI);
			else if (instr.OpCode.Code == CilCode.Ldind_U1)
				instr.ReplaceWith(CilOpCodes.Call, LdindU1);
			else if (instr.OpCode.Code == CilCode.Ldind_U4)
				instr.ReplaceWith(CilOpCodes.Call, LdindU4);
		}
	}

	static void Prepare(VMContext ctx)
	{
		ready = true;
		BuildCeq(ctx);
		BuildSub(ctx);
		BuildAdd(ctx);
		BuildLdindI(ctx);
		BuildLdindU1(ctx);
		BuildLdindU4(ctx);
	}

	static void BuildCeq(VMContext ctx)
	{
		var b = HighLevelIced.Get(ctx);
		var asm = b.asm;

		b.KickDnspy();
		asm.xor(eax, eax);
		asm.cmp(b.Arg1_64, b.Arg2_64);
		asm.sete(al);
		asm.movzx(eax, al);
		asm.ret();

		var me = ctx.AllocNativeMethod("ceq", MethodSignature.CreateStatic(ctx.PTR, ctx.PTR, ctx.PTR));
		me.Code = b.Compile();
		Ceq = me.Owner;
	}

	static void BuildAdd(VMContext ctx)
	{
		var b = HighLevelIced.Get(ctx);
		var asm = b.asm;

		b.KickDnspy();
		asm.lea(rax, __[b.Arg1_64 + b.Arg2_64]);
		asm.ret();

		var me = ctx.AllocNativeMethod("add", MethodSignature.CreateStatic(ctx.PTR, ctx.PTR, ctx.PTR));
		me.Code = b.Compile();
		Add = me.Owner;
	}

	static void BuildSub(VMContext ctx)
	{
		var b = HighLevelIced.Get(ctx);
		var asm = b.asm;

		b.KickDnspy();
		asm.mov(rax, b.Arg1_64);
		asm.sub(rax, b.Arg2_64);
		asm.ret();

		var me = ctx.AllocNativeMethod("sub", MethodSignature.CreateStatic(ctx.PTR, ctx.PTR, ctx.PTR));
		me.Code = b.Compile();
		Sub = me.Owner;
	}

	static void BuildLdindI(VMContext ctx)
	{
		var b = HighLevelIced.Get(ctx);
		var asm = b.asm;

		b.KickDnspy();
		asm.mov(rax, __qword_ptr[b.Arg1_64]);
		asm.ret();

		var me = ctx.AllocNativeMethod("ldindI", MethodSignature.CreateStatic(ctx.PTR, ctx.PTR));
		me.Code = b.Compile();
		LdindI = me.Owner;
	}

	static void BuildLdindU1(VMContext ctx)
	{
		var b = HighLevelIced.Get(ctx);
		var asm = b.asm;

		b.KickDnspy();
		asm.movzx(eax, __byte_ptr[b.Arg1_64]);
		asm.ret();

		var me = ctx.AllocNativeMethod("ldindU1", MethodSignature.CreateStatic(ctx.PTR, ctx.PTR));
		me.Code = b.Compile();
		LdindU1 = me.Owner;
	}

	static void BuildLdindU4(VMContext ctx)
	{
		var b = HighLevelIced.Get(ctx);
		var asm = b.asm;

		b.KickDnspy();
		asm.movzx(eax, __word_ptr[b.Arg1_64]);
		asm.ret();

		var me = ctx.AllocNativeMethod("ldindU4", MethodSignature.CreateStatic(ctx.PTR, ctx.PTR));
		me.Code = b.Compile();
		LdindU4 = me.Owner;
	}
}
