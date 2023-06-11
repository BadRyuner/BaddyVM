using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;

namespace BaddyVM.VM.Handlers;
internal class Objects
{
	internal static void Handle(VMContext ctx)
	{
		CallByAddress(ctx);
		AllocString(ctx);
		GetVirtFunc(ctx);
	}

	private static void CallByAddress(VMContext ctx) // unsafe 
	{
		var i = ctx.AllocManagedMethod("CallByAddress").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var adr)
		.NewLocal(ctx, out var argscount)
		.NewLocal(ctx, out var buf)
		.NewLocal(ctx, VMTypes.I1, out var isnewobj)
		.PopMem(ctx, adr).Save(adr)
		.DecodeCode(1).Save(argscount);
		i.LoadNumber(0).Save(isnewobj);
		i.Load(argscount).LoadNumber(0b1000_0000).And().LoadNumber(0b1000_0000).Compare()
			.IfTrue(() =>
			{
				i.Load(argscount).LoadNumber(0b1000_0000).Xor().Save(argscount);
				i.LoadNumber(1).Save(isnewobj);
			});

		var skipargs = new CilInstruction(CilOpCodes.Nop);

		i.Load(argscount).LoadNumber(0).Compare().IfTrue(() => i.Br(skipargs.CreateLabel()));

		var locals = new CilLocalVariable[ctx.MaxArgs];
		for (int x = 0; x < locals.Length; x++)
		{
			i.NewLocal(ctx, out locals[x]);
		}
		/*
		for(int x = 0; x < locals.Length; x++)
		{
			i.NewLocal(ctx, out locals[x]);
			i.Load(argscount).LoadNumber(x+1).LessOrEq().IfTrue(() => i.PopMem(ctx, buf).Save(locals[x]));
		}
		*/

		for (int x = 0; x < locals.Length; x++)
		{
			i.Load(argscount).LoadNumber(x + 1).Compare().IfTrue(() =>
			{
				var skipfirstarg = new CilInstruction(CilOpCodes.Nop);
				for(int z = 0; z <= x; z++)
				{
					if (z == 0)
						i.Load(isnewobj).LoadNumber(1).Compare().IfTrue(() =>
						{
							i.LoadFromLocalStorage(ctx).Save(locals[0]).Br(skipfirstarg.CreateLabel());
						});

					var loc = locals[z];
					i.PopMem(ctx, buf).Save(locals[z]);

					if (z == 0)
						i.Add(skipfirstarg);
				}
			});
		}

		var end = new CilInstruction(CilOpCodes.Nop);
		var endlabel = end.CreateLabel();

		i.Add(skipargs);
		i.Load(argscount).LoadNumber(0).Compare().IfTrue(() => i.Load(adr).Calli(ctx, 0, true).Save(adr).PushMem(ctx, adr, buf).Br(endlabel));

		for (int x = 0; x < locals.Length; x++)
		{
			i.Load(argscount).LoadNumber(x+1).Compare().IfTrue(() =>
			{
				i.Load(isnewobj).LoadNumber(1).Compare().IfBranch(() => // if newobj
				{
					i.Load(locals[0]);
					for (int z = x; z >= 1; z--)
						i.Load(locals[z]);
				}, () => // if just call
				{
					for (int z = x; z >= 0; z--)
						i.Load(locals[z]);
				});
				
				i.Load(adr).Calli(ctx, x+1, true).Save(adr).PushMem(ctx, adr, buf).Br(endlabel);
			});
		}

		i.Add(end);
		i.RegisterHandler(ctx, VMCodes.CallAddress);
	}

	private static void AllocString(VMContext ctx) // todo: add string crypt
	{
		var i = ctx.AllocManagedMethod("AllocString").CilMethodBody.Instructions
		.NewLocal(ctx, out var buf).NewLocal(ctx, out var length).NewLocal(ctx, out var str)
		//.NewLocal(ctx, out var ptr)
		.DecodeCode(4).Save(length)
		.CodePtr()
		.SkipCode(length)
		.Load(length)
		.AccessToVMTable(ctx)
		.LoadNumber(ctx.Transform((MetadataMember)ctx.core.module.DefaultImporter.ImportMethod(typeof(string).GetMethod("CreateStringForSByteConstructor", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic))))
		.Sum().DerefI()
		.Calli(ctx, 2, true).Save(str);
		//i.Load(str).LoadNumber(16).Sum().Save(ptr);

		//var exit = new CilInstruction(CilOpCodes.Nop);

		/*
		i.While(() =>
		{
			i.Load(length).LoadNumber(0).Compare().IfTrue(() => i.Br(exit.CreateLabel()));
			i.Load(ptr).DecodeCode(2).Set2();
			i.Load(ptr).LoadNumber(2).Sum().Save(ptr);
			i.Load(length).LoadNumber(1).Sub().Save(length);
		}); */

		//i.Add(exit);
		i.PushMem(ctx, str, buf);
		i.RegisterHandler(ctx, VMCodes.Ldstr);
	}

	private static void GetVirtFunc(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("GetVirtFunc").CilMethodBody.Instructions
		.NewLocal(ctx, out var res).NewLocal(ctx, out var buf);
		i.DecodeCode(2).Save(res)
		.PeekMem(ctx, res, res)
		.Load(res).DerefI()										// mov rdi, [rsi]
		.LoadNumber(0x40).DecodeCode(2).Sum().Sum().DerefI()	// mov rbx, [rdi+0x40+chunk]
		.DecodeCode(2).Sum().DerefI()							// mov rax, qword [rbx+FuncOffset]
		.Save(res).PushMem(ctx, res, buf);
		
		i.RegisterHandler(ctx, VMCodes.GetVirtFunc);
	}
}
