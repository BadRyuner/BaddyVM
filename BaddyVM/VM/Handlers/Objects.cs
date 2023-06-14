using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;

namespace BaddyVM.VM.Handlers;
internal class Objects
{
	internal static void Handle(VMContext ctx)
	{
		CallByAddress(ctx);
		SafeCall(ctx);
		AllocString(ctx);
		GetVirtFunc(ctx);
		CallInterface(ctx);
		//NewObjUnsafe(ctx);
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

	private static void SafeCall(VMContext ctx)
	{
		if (ctx.SafeCallTargets.Count == 0)
			return;

		var i = ctx.AllocManagedMethod("SafeCall").CilMethodBody.Instructions.NewLocal(ctx, out var buf);
		i.NewLocal(ctx, out var adr).PopMem(ctx, buf).Save(adr);
		i.NewLocal(ctx, VMTypes.U1, out var type).DecodeCode(1).Save(type);
		var maxargs = ctx.SafeCallTargets.Max(m => m.Signature.GetTotalParameterCount());
		var locals = new CilLocalVariable[maxargs];
		for (int x = 0; x < maxargs; x++)
			i.NewLocal(ctx, out locals[x]);

		var dict = new Dictionary<TypeSignature, CilLocalVariable>();
		var end = new CilInstruction(CilOpCodes.Nop);
		var endl = end.CreateLabel();

		i.NewLocal(ctx, out var saveretval);

		for(int x = 0; x < ctx.SafeCallTargets.Count; x++)
		{
			i.Load(type).LoadNumber(x).Compare().IfTrue(() =>
			{
				var target = ctx.SafeCallTargets[x];
				var args = target.Signature.GetTotalParameterCount();
				for (int z = 0; z < args; z++)
					i.PopMem(ctx, buf).Save(locals[z]);
				for (int z = args - 1; z >= 0; z--)
					i.Load(locals[z]);

				var retval = target.Signature.ReturnType;
				if (retval is GenericInstanceTypeSignature gits)
				{
					retval = gits.Fix(ctx);
				}

				//if (!dict.TryGetValue(retval, out var loc))
				//{
				//	i.NewLocal(retval, out loc);
				//	dict.Add(retval, loc);
				//}
				/*
				if (retval.GetImpliedMemoryLayout(false).Size <= 8)
				{
					i.Load(adr).Calli(ctx, target.Signature).Save(adr).PushMem(ctx, adr, buf);
					i.Br(endl);
				}
				else
				{
					i.Load(adr).Calli(ctx, target.Signature).Save(loc);
					var size = retval.GetImpliedMemoryLayout(false).Size;
					i.LoadRef(loc).LoadNumber((int)size).Call(ctx.Allocator).Save(adr).PushMem(ctx, adr, buf);
					i.Br(endl);
				} */

				//i.Load(adr).Calli(ctx, target.Signature).Save(saveretval);
				i.Load(adr).Calli(ctx, ((MethodSignature)target.Signature).GetTotalParameterCount(), true).Save(saveretval);
				var size = retval.GetImpliedMemoryLayout(false).Size;
				i.Load(saveretval).LoadNumber((int)size).Call(ctx.Allocator).Save(adr).PushMem(ctx, adr, buf);
				i.Br(endl);
			});
		}

		i.Add(end);
		i.RegisterHandler(ctx, VMCodes.SafeCall);
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

	private static void CallInterface(VMContext ctx) // broken
	{
		if (ctx.InterfaceCalls.Count == 0)
			return;

		var i = ctx.AllocManagedMethod("CallInterface").CilMethodBody.Instructions
			.NewLocal(ctx, out var isConstrained).NewLocal(ctx, out var refcontainer)
			.NewLocal(ctx, out var buf).NewLocal(ctx, out var target)
			.NewLocal(ctx, out var idx);

		i.DecodeCode(2).Save(idx);

		i.DecodeCode(1).LoadNumber(1).Compare().IfTrue(() =>
		{
			i.PopMem(ctx, buf).Save(isConstrained).LoadRef(isConstrained).Save(refcontainer);
		});

		var maxargs = ctx.InterfaceCalls.Max(m => m.Signature.GetTotalParameterCount());

		var args = new CilLocalVariable[maxargs];
		for(int x = 0; x < maxargs; x++)
			i.NewLocal(ctx, out args[x]);

		for(int z = 0; z < maxargs; z++)
		{
			var interf = ctx.InterfaceCalls[z];
			i.LoadNumber(z).Load(idx).Compare().IfTrue(() =>
			{
				var reqargs = interf.Signature.GetTotalParameterCount();
				for(int x = 0; x < reqargs; x++)
				{
					i.PopMem(ctx, buf).Save(args[x]);
					if (x == reqargs - 1)
						i.Load(args[x]).Save(target);
				}
				for (int x = 0; x < reqargs; x++)
					i.Load(args[x]);

				//i.Load(isConstrained).LoadNumber(1).Cgt().IfTrue(() => i.Load(refcontainer).Save(target));
				i.Load(target);
				i.Add(CilOpCodes.Ldvirtftn, ctx.core.module.DefaultImporter.ImportMethod(interf));
				//i.Add(CilOpCodes.Ldftn, ctx.core.module.DefaultImporter.ImportMethod(interf));
				bool ret = interf.Signature.ReturnsValue;
				i.Calli(ctx, reqargs, ret);
				if (ret)
					i.Save(target).PushMem(ctx, target, buf);
			});
		}

		i.RegisterHandler(ctx, VMCodes.CallInterface);
	}

	/* // broken :(
	private static void NewObjUnsafe(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("NewObjUnsafe").CilMethodBody.Instructions
			.NewLocal(ctx, out var buf)
			.NewLocal(ctx, out var type);

		i.DecodeCode(2).Save(type);

		var maxargs = ctx.NewObjUnsafeData.Keys.Max(m => m.Signature.GetTotalParameterCount() - 1);

		var locals = new CilLocalVariable[maxargs];
		for (int x = 0; x < locals.Length; x++)
			i.NewLocal(ctx, out locals[x]);

		var exit = new CilInstruction(CilOpCodes.Nop);
		var exitl = exit.CreateLabel();

		foreach (var x in ctx.NewObjUnsafeData)
		{
			i.LoadNumber(x.Value).Load(type).Compare().IfTrue(() =>
			{
				var args = x.Key.Signature.GetTotalParameterCount() - 1;
				for(int z = 0; z < args; z++)
					i.PopMem(ctx, buf).Save(locals[z]);
				//for(int z = args-1; z >= 0; z--)
				for (int z = 0; z < args; z++)
				{
					if (x.Key.Signature.ParameterTypes[z].IsValueType)
						i.LoadRef(locals[z]).DerefI();
					else
					{
						i.LoadRef(locals[z]);
						i.Add(CilOpCodes.Ldind_Ref);
					}
				}
				i.NewObj(x.Key);
				//i.Add(CilOpCodes.Pop);
				i.Save(type).PushMem(ctx, type, buf).Br(exitl);
			});
		}
		i.Add(exit);
		i.RegisterHandler(ctx, VMCodes.NewObjUnsafe);
	} */
}