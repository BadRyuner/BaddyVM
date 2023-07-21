using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using BaddyVM.VM.Utils;
using Iced.Intel;

namespace BaddyVM.VM.Protections.Transformations;
internal static class SlowStringCompare
{
	static Dictionary<string, MethodDefinition> cache = new(4);

	internal unsafe static MethodDefinition GetMethod(VMContext ctx, string str)
	{
		if (cache.TryGetValue(str, out var res))
			return res;

		var fn = ctx.AllocNativeMethod(str, MethodSignature.CreateStatic(ctx.PTR, ctx.PTR));
		var length = str.Length*2;
		var start = length;
		var coder = HighLevelIced.Get(ctx);
		coder.KickDnspy();

		var bad = coder.asm.CreateLabel();
		coder.asm.cmp(AssemblerRegisters.__dword_ptr[coder.Arg1_64 + 8], str.Length); // arg1->length == str.length
		coder.asm.jne(bad);

		fixed(char* c = str)
		{
			byte* p = (byte*)c;
			while (length > 0)
			{
				var pos = start - length;
				if (length - 4 >= 0)
				{
					uint chars = *(uint*)(p + pos);
					coder.asm.cmp(AssemblerRegisters.__dword_ptr[coder.Arg1_64 + (pos + 12)], chars);
					coder.asm.jne(bad);
					length -= 4;
				}
				else if (length - 2 >= 0)
				{
					uint chars = *(ushort*)(p + pos);
					coder.asm.cmp(AssemblerRegisters.__word_ptr[coder.Arg1_64 + (pos + 12)], chars);
					coder.asm.jne(bad);
					length -= 2;
				}
				else
					throw new NotImplementedException(); // imao, any string can be / by 2.
			}
		}

		coder.asm.mov(AssemblerRegisters.eax, 1); // result == true
		coder.Return();

		coder.asm.Label(ref bad);
		coder.asm.mov(AssemblerRegisters.eax, 0); // result == false
		coder.Return();

		fn.Code = coder.Compile();

		cache.Add(str, fn.Owner);
		return fn.Owner;
	}
}