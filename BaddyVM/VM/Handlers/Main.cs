using AsmResolver;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.Code;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using static Iced.Intel.AssemblerRegisters;

namespace BaddyVM.VM.Handlers;
internal static class Main
{
	internal static void Handle(VMContext ctx, CilInstructionCollection cctor)
	{
		Ret(ctx);

		var builder = HighLevelIced.Get(ctx);
		var asm = builder.asm;

		var instructionRunner = rax;
		var code = builder.Arg1_64;
		var instructions = r10;

		/* 🚀 BLAZING 🚀 FAST 🚀 */

		asm.mov(instructions, 123123123123); // get 
		asm.xor(rax,rax); // clear rax
		asm.mov(al, __byte_ptr[code]); // get opcode
		asm.lea(code, __[code+1]); // advance code ptr
		asm.lea(instructionRunner, __[instructions + instructionRunner * 8]); // runner = instructions[opcode * 8]
		asm.jmp(__qword_ptr[instructionRunner]);

		var compiled = builder.Compile();

		uint replaceFunctionsPointer = 0;
		unsafe
		{
			fixed(byte* p = compiled) 
			{
				var pp = p;
				while(true)
				{
					if (*(long*)pp == 123123123123)
					{
						break;
					}
					replaceFunctionsPointer++;
					pp++;
				}
			}
		}

		var fnptrs = new DataSegment(new byte[8 * byte.MaxValue]);
		ctx.FunctionsPointers = fnptrs;
		var sym = new Symbol(fnptrs.ToReference());

		var body = ctx.Router.NativeMethodBody;
		body.Code = compiled;
		body.AddressFixups.Add(new AddressFixup(replaceFunctionsPointer, AddressFixupType.Absolute64BitAddress, sym));
		
		// RETURNS FUNCTIONS POINTERS DATASEGMENT
		body = ctx.AllocNativeMethod("GetFunPtrs", MethodSignature.CreateStatic(ctx.PTR));
		builder = HighLevelIced.Get(ctx);
		asm = builder.asm;
		asm.mov(rax, 123123123123);
		asm.ret();
		compiled = builder.Compile();

		replaceFunctionsPointer = 0;
		unsafe
		{
			fixed (byte* p = compiled)
			{
				var pp = p;
				while (true)
				{
					if (*(long*)pp == 123123123123)
					{
						break;
					}
					replaceFunctionsPointer++;
					pp++;
				}
			}
		}
		body.Code = compiled;
		body.AddressFixups.Add(new AddressFixup(replaceFunctionsPointer, AddressFixupType.Absolute64BitAddress, sym));

		var movptrs = ctx.AllocManagedMethod("MovPtrs", MethodSignature.CreateStatic(ctx.core.module.CorLibTypeFactory.Void));
		cctor.Call(movptrs);
		var i = movptrs.CilMethodBody.Instructions;

		var handlers = ctx.Handlers.OrderBy(x => Random.Shared.Next());

		i.Call(body.Owner); // push datasegment ptr
		
		foreach(var handler in handlers) 
		{
			i.Dup().LoadNumber(handler.Key * 8).Sum().Ldftn(handler.Value).Set8(); // DS[byte]=handler
		}

		i.Pop();
		i.Ret();
		i.CalculateOffsets();
		Protections.General.Recontrol.ignore.Add(i.Owner);
	}

	internal static void Ret(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Ret").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var buf);

		//i.GetInstanceID(ctx).LoadNumber(0).LoadNumber(1).GetRCResovler(ctx).Calli(ctx, 3, false); // flush all gchandles

		i.CheckIfNoRet(ctx).IfBranch(
			() => /*if no ret*/
			{
				i.LoadNumber(-1)
				.LoadLocalStackHeap(ctx).FreeGlobalHide(ctx).Arg1().FreeGlobalHide(ctx)
				.Ret();
			},
			() => /*if ret*/
			{
				i.PopMem(ctx, buf); // get result
				i.DecodeCode(2).Save(buf); // get size
				i.Load(buf).LoadNumber(0).Compare().IfBranch(() =>
				{
					i.LoadLocalStackHeap(ctx).FreeGlobalHide(ctx).Arg1().FreeGlobalHide(ctx); // free vm local method instance
					i.Ret(); // return 1/2/4/8 byte-sized values
				}, 
				() =>
				{
					i.Load(buf)
					.MoveToGlobalMem(ctx) // move to global mem
					.LoadLocalStackHeap(ctx).FreeGlobalHide(ctx).Arg1().FreeGlobalHide(ctx) // free vm local method instance
					.Ret(); // return ptr to global mem
				});
			})
		.RegisterHandler(ctx, VMCodes.Ret);
	}
}
