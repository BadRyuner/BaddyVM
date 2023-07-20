using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.Code;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.File;
using BaddyVM.VM.Utils;
using Iced.Intel;
using Reloaded.Memory.Extensions;

namespace BaddyVM.VM.Protections;
internal static class NativeString
{
	static List<DataSegment> strings = new(8);
	
	internal static MethodDefinition Make(VMContext ctx, string str)
	{
		var bytes = new byte[14 + str.Length * 2]; // 8 for handle, 4 for length, N for content, 2 for zero.
		unsafe
		{
			fixed(byte* p = &bytes[8])
			{
				*(int*)p = str.Length;
				str.AsSpan().CopyTo(MemoryExtensions.AsSpan(bytes, 12).CastFast<byte, char>()); // speeeEEEEEEEEEEEEeeed
			}
		}
		var ds = new DataSegment(bytes);
		strings.Add(ds);

		var asm = HighLevelIced.Get(ctx);
		asm.KickDnspy();
		asm.MoveLongToResult(123123123123123);
		asm.Return();
		bytes = asm.Compile();
		uint offset = 0;
		unsafe
		{
			fixed(byte* pp = &bytes[0])
			{
				var p = pp;
				while(true)
				{
					var l = (long*)p;
					if (*l == 123123123123123)
						break;
					p++;
					offset++;
				}
			}
		}

		var fn = ctx.AllocNativeMethod("a", MethodSignature.CreateStatic(ctx.PTR));
		fn.Code = bytes;
		fn.AddressFixups.Add(new AddressFixup(offset, AddressFixupType.Absolute64BitAddress, new Symbol(ds.ToReference())));
		return fn.Owner;
	}

	internal static void SetHandles(VMContext ctx)
	{
		if (strings.Count == 0) return;

		var asm = HighLevelIced.Get(ctx);
		asm.KickDnspy();

		var hint = 123123123123123;

		foreach (var i in strings)
		{
			asm.asm.mov(AssemblerRegisters.rax, hint);
			asm.asm.mov(AssemblerRegisters.__qword_ptr[AssemblerRegisters.rax], asm.Arg1_64);
			hint++;
		}

		var fn = ctx.AllocNativeMethod("a", MethodSignature.CreateStatic(ctx.core.module.CorLibTypeFactory.Void, ctx.PTR));
		fn.Code = asm.Compile();

		hint = 123123123123123;
		foreach (var i in strings)
		{
			uint codeOffset = 0;
			unsafe
			{
				fixed (byte* pp = &fn.Code[0])
				{
					var p = pp;
					while (true)
					{
						var l = (long*)p;
						if (*l == hint)
							break;
						codeOffset++;
						p++;
					}
				}
			}
			fn.AddressFixups.Add(new AddressFixup(codeOffset, AddressFixupType.Absolute64BitAddress, new Symbol(i.ToReference())));
			hint++;
		}

		var ctor = ctx.core.module.GetOrCreateModuleConstructor();
		var instr = ctor.CilMethodBody.Instructions;
		var loc = new CilLocalVariable(ctx.core.module.DefaultImporter.ImportType(typeof(RuntimeTypeHandle)).ToTypeSignature());
		ctor.CilMethodBody.LocalVariables.Add(loc);
		instr.Insert(0, new CilInstruction(CilOpCodes.Ldtoken, ctx.core.module.CorLibTypeFactory.String.ToTypeDefOrRef()));
		instr.Insert(1, new CilInstruction(CilOpCodes.Stloc, loc));
		instr.Insert(2, new CilInstruction(CilOpCodes.Ldloca, loc));
		instr.Insert(3, new CilInstruction(CilOpCodes.Call, ctx.core.module.DefaultImporter.ImportMethod(typeof(RuntimeTypeHandle).GetMethod("get_Value"))));
		instr.Insert(4, new CilInstruction(CilOpCodes.Call, fn.Owner));
	}

	internal static void MovStrings(SegmentBuilder builder)
	{
		if (strings.Count == 0) return;

		foreach (var i in strings)
		{
			builder.Add(i);
		}
		strings.Clear();
	}
}
