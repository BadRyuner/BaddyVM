using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Memory;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaddyVM.VM;
internal ref struct LocalHeapMap
{
	internal VMContext ctx;
	internal Dictionary<object, ushort> offsets = new(16);
	internal ushort maxsize = 0;

	public LocalHeapMap(VMContext ctx) => this.ctx = ctx;

	internal ushort Get(object obj) => offsets[obj];
	internal void Register(MethodDefinition md)
	{
		ushort start = (ushort)ctx.layout.VMHeaderEnd;
		if (!md.IsStatic)
		{
			offsets.Add(new CilInstruction(CilOpCodes.Ldarg_0).GetParameter(md.Parameters), start);
			start += 8;
		}
		foreach(var p in md.Parameters)
		{
			offsets.Add(p, start);
			start += 8;
		}
		foreach(var l in md.CilMethodBody.LocalVariables)
		{
			offsets.Add(l, start);
			start += l.VariableType.ElementType == ElementType.ValueType ? (ushort)l.VariableType.GetImpliedMemoryLayout(false).Size : (ushort)8;
		}
		maxsize = start;
	}
}
