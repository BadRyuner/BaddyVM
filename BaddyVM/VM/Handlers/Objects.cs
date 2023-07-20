using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using Iced.Intel;

namespace BaddyVM.VM.Handlers;
internal class Objects
{
	internal static void Handle(VMContext ctx)
	{
		CallManaged(ctx);
		CallByAddress(ctx);
		SafeCall(ctx);
		AllocString(ctx);
		GetVirtFunc(ctx);
		CallInterface(ctx);
		//NewObjUnsafe(ctx);
		Box(ctx);
		Unbox(ctx);
	}

	// stack: [method, args]
	// bytes: [argsCount, isRet]
	private static void CallManaged(VMContext ctx) // safer
	{
		if (ctx.IsNet6()) return;

		var i = ctx.AllocManagedMethod("CallManaged").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var buffer).NewLocal(ctx, out var buf).NewLocal(ctx, out var buf2)
		 .NewLocal(ctx, out var args).NewLocal(ctx, out var argsPtr)
		 .NewLocal(ctx, out var sig).NewLocal(ctx, out var flags);
		i.DecodeCode(2).Save(args);
		i.DecodeCode(1).Save(flags);

		i.PopMem(ctx, sig).Save(sig);

		var exit = new CilInstruction(CilOpCodes.Nop);
		
		i.Load(args).LoadNumber(0).Compare().IfBranch(() => i.Br(exit), () => i.Load(args).Stackalloc().Save(argsPtr)); // no exceptions when args == 0
		
		i.While(() =>
		{
			i.Load(args).LoadNumber(0).Compare().IfTrue(() => i.Br(exit));
			i.Dec(args, 8);
			i.DecodeCode(1).LoadNumber(0).Compare().IfBranch(() => i.Load(argsPtr).Load(args).Sum().PopMemAsRef(ctx, buffer).Set8(), // argsPtr[args] = *stack--
				() =>
				{
					var pStruct = buffer;
					i.DecodeCode(2).Dup().Save(buf).Stackalloc().Save(pStruct);
					//i.Load(pStruct).LoadNumber(8).Sum().PopMem(ctx, buf2).Load(buf);
					i.Load(pStruct).PopMem(ctx, buf2).Load(buf);
					i.CallHide(ctx, ctx.MemCpy);
					i.Load(argsPtr).Load(args).Sum().Load(pStruct).Set8();
				});
		});
		
		i.Add(exit);

		// NET6.0:	 object? target, in Span<object?> arguments, Signature sig, bool constructor, bool wrapExceptions
		// Net6.0:	 Not Supported.
		// NET7.0>=: object  target, void**           arguments, Signature sig, bool constructor
		// Net7.0>=: Works good
		var pseudoCall = ctx.core.module.DefaultImporter.ImportMethod(typeof(RuntimeMethodHandle).GetMethod("InvokeMethod", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
		

		i.Load(flags).CheckBit(0b10).IfBranch(() => i.PopMem(ctx, buffer), () => i.LoadNumber(0)); // target
		i.Load(argsPtr); // arguments
		i.Load(sig); // sig
		i.LoadNumber(0); // constructor = false
		i.CallHide(ctx, pseudoCall);

		var result = args;
		i.Save(result);
		i.Load(flags).CheckBit(0b100).IfTrue(() => i.Load(result).LoadNumber(8).Sum().Save(result)); // is struct
		i.Load(flags).CheckBit(0b1000).IfTrue(() => i.Load(result).DerefI().Save(result)); // is packed struct
		i.Load(flags).CheckBit(0b01).IfTrue(() => i.PushMem(ctx, result, buffer)); // push return value
		i.RegisterHandler(ctx, VMCodes.CallManaged);
	}

	private static void CallByAddress(VMContext ctx) // unsafe 
	{
		var i = ctx.AllocManagedMethod("CallByAddress").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var adr)
		.NewLocal(ctx, out var argscount)
		.NewLocal(ctx, out var buf)
		.NewLocal(ctx, out var floatByte)
		.NewLocal(ctx, VMTypes.I1, out var isnewobj)
		.PopMem(ctx, adr).Save(adr)
		.DecodeCode(1).Save(argscount).DecodeCode(1).Save(floatByte);
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

		var skipFloat = new CilInstruction(CilOpCodes.Nop);

		i.Load(floatByte).LoadNumber(0).Compare().IfTrue(() => i.Br(skipFloat));

		var setFloatSig = MethodSignature.CreateStatic(ctx.core.module.CorLibTypeFactory.Void, ctx.PTR);
		var setFloat = ctx.AllocNativeMethod("SetXmm0", setFloatSig);
		var w = HighLevelIced.Get(ctx);
		w.asm.movq(AssemblerRegisters.xmm0, w.Arg1_64);
		w.Return();
		setFloat.Code = w.Compile();

		if (locals.Length >= 1)
		{
			i.Load(floatByte).CheckBit(0b1).IfTrue(() => i.Load(locals[0]).Call(setFloat.Owner));
		}

		if (locals.Length >= 2)
		{
			setFloat = ctx.AllocNativeMethod("SetXmm1", setFloatSig);
			w = HighLevelIced.Get(ctx);
			w.asm.movq(AssemblerRegisters.xmm1, w.Arg1_64);
			w.Return();
			setFloat.Code = w.Compile();

			i.Load(floatByte).CheckBit(0b10).IfTrue(() => i.Load(locals[1]).Call(setFloat.Owner));
		}

		if (locals.Length >= 3)
		{
			setFloat = ctx.AllocNativeMethod("SetXmm2", setFloatSig);
			w = HighLevelIced.Get(ctx);
			w.asm.movq(AssemblerRegisters.xmm2, w.Arg1_64);
			w.Return();
			setFloat.Code = w.Compile();

			i.Load(floatByte).CheckBit(0b100).IfTrue(() => i.Load(locals[2]).Call(setFloat.Owner));
		}

		if (locals.Length >= 4)
		{
			setFloat = ctx.AllocNativeMethod("SetXmm3", setFloatSig);
			w = HighLevelIced.Get(ctx);
			w.asm.movq(AssemblerRegisters.xmm3, w.Arg1_64);
			w.Return();
			setFloat.Code = w.Compile();

			i.Load(floatByte).CheckBit(0b1000).IfTrue(() => i.Load(locals[3]).Call(setFloat.Owner));
		}

		i.Add(skipFloat);

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
		{
			setFloat = ctx.AllocNativeMethod("GetXmm0", MethodSignature.CreateStatic(ctx.PTR));
			w = HighLevelIced.Get(ctx);
			w.asm.movq(AssemblerRegisters.rax, AssemblerRegisters.xmm0);
			w.Return();
			setFloat.Code = w.Compile();
			var floatRet = adr;
			i.Load(floatByte).CheckBit(0b1_0000).IfTrue(() => i.Call(setFloat.Owner).Save(floatRet).OverrideMem(ctx, floatRet));
		}
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

		for (int x = 0; x < ctx.SafeCallTargets.Count; x++)
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

				i.NewLocal(retval, out var ret);

				i.Load(adr).Calli(ctx, ((MethodSignature)target.Signature).GetTotalParameterCount(), retval).Save(ret)
					.LoadRef(ret).Save(saveretval);
				var size = retval.GetImpliedMemoryLayout(false).Size;
				i.Load(saveretval).LoadNumber((int)size).CallHide(ctx, ctx.Allocator).Save(adr).PushMem(ctx, adr, buf);
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
		.DecodeCode(4).Save(length);

		bool isNet6 = ctx.IsNet6();

		if (isNet6) i.LoadNumber(0); // pseudo this for ctor
		i.CodePtr() // ptr
		.SkipCode(length)
		.AccessToVMTable(ctx);
		
		var ctor = isNet6 ? ctx.core.module.DefaultImporter.ImportMethod(typeof(string).GetMethod("Ctor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, new[] { typeof(char*) }))
			: ctx.core.module.DefaultImporter.ImportMethod(typeof(string).GetMethod("Ctor", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, new[] { typeof(char*) }));

		i.LoadNumber(ctx.Transform((MetadataMember)ctor));
		i.Sum().DerefI().Calli(ctx, isNet6 ? 2 : 1, true).Save(str); 

		i.PushMem(ctx, str, buf);
		i.RegisterHandler(ctx, VMCodes.Ldstr);
	}

	private static void GetVirtFunc(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("GetVirtFunc").CilMethodBody.Instructions
		.NewLocal(ctx, out var obj).NewLocal(ctx, out var slot);
		var offset = obj;

		var rmh = typeof(RuntimeMethodHandle);
		var rth = typeof(RuntimeTypeHandle);
		var _gma = rth.GetMethod("GetMethodAt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
		var _gmaret = ctx.core.module.DefaultImporter.ImportTypeSignature(_gma.ReturnType);
		var getmethodat = ctx.core.module.DefaultImporter.ImportMethod(_gma);
		var getfnptr = ctx.core.module.DefaultImporter.ImportMethod(rmh.GetMethod("GetFunctionPointer", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
		var gettype = ctx.core.module.DefaultImporter.ImportMethod(typeof(object).GetMethod("GetType"));

		i.PopMem(ctx, slot).Save(slot);
		i.DecodeCode(2).Save(offset)
		.PeekMem(ctx, offset, obj);

		var method = slot;
		var handle = method;
		//i.NewLocal(_gmaret, out var handle);
		var type = obj;
		i.Load(obj).Call(gettype).Save(type).Load(type).Load(slot).CallHide(ctx, getmethodat).Save(handle);
		//i.Load(obj).Call(gettype).Load(slot).Call(getmethodat).Save(handle);
		i.Load(handle).CallHide(ctx, getfnptr).Save(method);
		//i.Load(handle).Call(getfnptr).Save(method);
		var buf = obj;
		i.PushMem(ctx, method, buf);
		
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

	private static void Box(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Box").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var value).NewLocal(ctx, out var handle)
		 .NewLocal(ctx, out var size).NewLocal(ctx, out var boxed);

		i.PopMem(ctx, handle).Save(handle);
		i.PopMem(ctx, value).Save(value);

		i.LoadNumber(0).LoadNumber(8).Load(size).Sum().CallHide(ctx, ctx.Allocator).Save(boxed).PushMem(ctx, boxed, size);
		i.Load(boxed).Load(handle).Set8();
		i.IncPtr(boxed);

		var end = new CilInstruction(CilOpCodes.Nop);

		i.DecodeCode(2).Save(size);

		i.LoadNumber(1).Load(size).Compare().IfTrue(() => i.Load(boxed).Load(value).Set1().Br(end));
		i.LoadNumber(2).Load(size).Compare().IfTrue(() => i.Load(boxed).Load(value).Set2().Br(end));
		i.LoadNumber(4).Load(size).Compare().IfTrue(() => i.Load(boxed).Load(value).Set4().Br(end));
		i.LoadNumber(8).Load(size).Compare().IfTrue(() => i.Load(boxed).Load(value).Set8().Br(end));

		// unalligned structs already passed as ref
		i.Load(value).Load(boxed).Load(size).CallHide(ctx, ctx.MemCpy);

		i.Add(end);
		i.RegisterHandler(ctx, VMCodes.Box);
	}

	private static void Unbox(VMContext ctx)
	{
		var i = ctx.AllocManagedMethod("Unbox").CilMethodBody.Instructions;
		i.NewLocal(ctx, out var what).NewLocal(ctx, out var size);
		i.DecodeCode(2).Save(size);
		i.PopMem(ctx, what).LoadNumber(8).Sum().Save(what); // Get RawObject from boxed struct

		var buf = size;
		var result = what;

		var end = new CilInstruction(CilOpCodes.Nop);

		i.Load(size).LoadNumber(8).Compare().IfTrue(() => i.Load(what).Deref8().Save(result).PushMem(ctx, result, buf).Br(end));
		i.Load(size).LoadNumber(4).Compare().IfTrue(() => i.Load(what).DerefI4().Save(result).PushMem(ctx, result, buf).Br(end));
		i.Load(size).LoadNumber(2).Compare().IfTrue(() => i.Load(what).DerefI2().Save(result).PushMem(ctx, result, buf).Br(end));
		i.Load(size).LoadNumber(1).Compare().IfTrue(() => i.Load(what).DerefI1().Save(result).PushMem(ctx, result, buf).Br(end));

		i.PushMem(ctx, result, buf);

		i.Add(end);
		i.RegisterHandler(ctx, VMCodes.Unbox);
	}
}