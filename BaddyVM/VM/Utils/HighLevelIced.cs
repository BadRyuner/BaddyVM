using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BaddyVM.VM.Utils;
internal ref struct HighLevelIced
{
	internal Assembler asm;

	internal AssemblerRegister64 Rax => rax;
	internal AssemblerRegister64 Arg1_64 => rcx;
	internal AssemblerRegister64 Arg2_64 => rdx;
	internal AssemblerRegister64 Arg3_64 => r8;
	internal AssemblerRegister64 Arg4_64 => r9;

	private Dictionary<Label, byte[]> DataToEnd;
	private VMContext ctx;

	internal static HighLevelIced Get(VMContext ctx) => new HighLevelIced() { 
		asm = new Assembler(64), 
		ctx = ctx,
		DataToEnd = new(4)
	};

	internal void KickDnspy()
	{
		var l = asm.CreateLabel();
		asm.lea(rax, __[l]);
		asm.jmp(rax);
		asm.Label(ref l);
	}

	internal void MoveLongToResult(Label l) => asm.mov(rax, __qword_ptr[l]);

	internal void MovePtrToResult(Label l) => asm.lea(rax, __qword_ptr[l]);

	internal void JmpReg(AssemblerRegister64 reg) => asm.jmp(reg);

	internal void CallReg(AssemblerRegister64 reg) => asm.call(reg);

	internal void ClearReg(AssemblerRegister32 reg) => asm.xor(reg, reg);

	internal void Return() => asm.ret();

	internal Label AddData(long value)
	{
		var l = asm.CreateLabel();
		DataToEnd.Add(l, BitConverter.GetBytes(value));
		return l;
	}

	internal Label AddData(byte[] value)
	{
		var l = asm.CreateLabel();
		DataToEnd.Add(l, value);
		return l;
	}

	internal byte[] Compile()
	{
		foreach(var l in DataToEnd)
		{
			var label = l.Key;
			asm.Label(ref label);
			asm.db(l.Value, 0, l.Value.Length);
		}

		using var stream = new MemoryStream();
		asm.Assemble(new StreamCodeWriter(stream), 0);
		return stream.ToArray();
	}
}
