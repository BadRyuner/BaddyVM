using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.DataFlow;
using Echo.Platforms.AsmResolver;

namespace BaddyVM.VM.Protections.General;
internal static class Recontrol
{
	static DataFlowGraph<CilInstruction> dfg;
	internal static List<CilMethodBody> ignore = new(2);

	internal static void Apply(VMContext ctx, CilMethodBody body)
	{
		if (body.Owner.IsConstructor) return;
		if (ignore.Contains(body)) return;
		if (body.Instructions.Any(i => i.OpCode.Code is CilCode.Jmp or CilCode.Throw)) 
			return; // still hasn't been fixed in Echo
		body.Instructions.CalculateOffsets();
		var flow = body.ConstructSymbolicFlowGraph(out dfg);
		var blocks = flow.ConstructBlocks();
		var _all = blocks.GetAllBlocks().SelectMany(Splitter).ToList();
		if (_all.Count == 0) 
			return;
		var allblocks = _all.OrderBy(s => Random.Shared.Next()).ToList();
		//if (allblocks.Any(b => dfg.Nodes[b.Instructions[0].Offset].StackDependencies.Count != 0)) 
		//	return;
		var i = body.Instructions;
		i.ExpandMacros();
		i.Clear();

		var switcher = new CilLocalVariable(ctx.PTR);
		body.LocalVariables.Add(switcher);
		i.LoadNumber((int)blocks.GetFirstBlock().Offset).Save(switcher);

		var header = i.Add(CilOpCodes.Nop);

		foreach(var x in allblocks)
		{
			i.Load(switcher).LoadNumber((int)x.Offset).Compare().IfTrue(() => i.Br(x.Instructions[0]));
		}

		foreach(var x in allblocks)
		{
			i.AddRange(x.Instructions);
			var fc = x.Footer.OpCode.FlowControl;
			if (fc != CilFlowControl.Return && fc != CilFlowControl.Throw)
			{
				var indexof = _all.IndexOf(x);
				i.LoadNumber((int)_all[indexof + 1].Offset).Save(switcher);
				i.Br(header);
			}
		}
		//i.CalculateOffsets();
		//i.OptimizeMacros();
		//body.BuildFlags = (CilMethodBodyBuildFlags)0;
	}

	private static IEnumerable<BasicBlock<CilInstruction>> Splitter(BasicBlock<CilInstruction> block)
	{
		List<BasicBlock<CilInstruction>> list = new(4);

		var selected = 0;

		for(int i = 0; i < block.Instructions.Count; i++)
		{
			var instruction = block.Instructions[i];
			bool good = false;

			var data = dfg.Nodes[instruction.Offset];

			//Console.WriteLine($"{i}) {instruction.OpCode} {instruction.Operand} {data.InDegree} {data.OutDegree} {data.StackDependencies.Count}");

			//Console.WriteLine($"{i}) ({instruction.Offset.ToString("X2")}) {instruction.OpCode} {instruction.GetStackPushCount()} {data.InDegree}");

			if (instruction.GetStackPushCount() == 0 && data.InDegree == 0)
			{
				good = true;
			}

			if (good && selected > 3)
			{
				var first = block.Instructions[i - selected];
				list.Add(new BasicBlock<CilInstruction>(first.Offset, block.Instructions.Skip(i - selected).Take(selected+1).ToArray()));
				selected = 0;
			}
			else
				selected++;
		}

		if (selected != 0)
		{
			var offset = block.Instructions.Count - selected;
			if (offset == block.Instructions.Count)
				offset = block.Instructions.Count - 1;
			var first = block.Instructions[offset];
			list.Add(new BasicBlock<CilInstruction>(first.Offset, block.Instructions.Skip(offset).ToArray()));
		}

		return list;
	}
}
