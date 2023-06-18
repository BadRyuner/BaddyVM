using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BaddyVM.VM.Utils;
internal ref struct HighLevelIced
{
	internal Assembler asm;

	private Dictionary<Label, byte[]> DataToEnd;
	private VMContext ctx;

	internal static HighLevelIced Get(VMContext ctx) => new HighLevelIced() { 
		asm = new Assembler(64), 
		ctx = ctx,
		DataToEnd = new(4)
	};

	internal void MoveLongToResult(Label l)
	{
		asm.mov(rax, __qword_ptr[l]);
	}

	internal void Return() => asm.ret();

	internal Label AddData(long value)
	{
		var l = asm.CreateLabel();
		DataToEnd.Add(l, BitConverter.GetBytes(value));
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
