﻿using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using Reloaded.Assembler;
using Reloaded.Assembler.Definitions;
using System.Diagnostics;
using System.Text;

namespace BaddyVM.VM;
internal ref struct VMWriter
{
	public VMWriter() { }

	internal VMContext ctx = null;
	internal Assembler assembler = null;
	internal StringBuilder buffer = null;
	internal Dictionary<CilExceptionHandler, string> anonlabels = new(4);
	internal List<int> AlreadyMarkedOffsets = new();
	internal uint anonid = 0;

	internal void Header()
	{
		buffer.AppendLine("use64\nlea rax, [CodeStart]\nret\nCodeStart:");
	}

	internal void Mark(int offset)
	{
		buffer.AppendLine($"IL_{offset}:");
		AlreadyMarkedOffsets.Add(offset);
	}

	internal void Nop() {}

	#region jumps
	internal void Switch(int[] dests)
	{
		buffer.Code(ctx, VMCodes.Switch).Int(dests.Length - 1);
		for (int i = 0; i < dests.Length; i++)
			buffer.LabelOffset(dests[i]);
	}
	internal void Br(int dest) => buffer.Code(ctx, VMCodes.Br).LabelOffset(dest);
	internal void Brtrue(int dest) => buffer.Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void Brfalse(int dest) => buffer.Code(ctx, VMCodes.Brfalse).LabelOffset(dest);
	internal void Bne(int dest) => buffer.Code(ctx, VMCodes.Ceq).Code(ctx, VMCodes.Push4).Int(0).Code(ctx, VMCodes.Ceq).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void Beq(int dest) => buffer.Code(ctx, VMCodes.Ceq).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void Bgt(int dest) => buffer.Code(ctx, VMCodes.Cgt).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void BgtUn(int dest) => buffer.Code(ctx, VMCodes.Cgt_Un).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void Bge(int dest) => buffer.Code(ctx, VMCodes.Clt).Code(ctx, VMCodes.Push4).Int(0).Code(ctx, VMCodes.Ceq).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void BgeUn(int dest) => buffer.Code(ctx, VMCodes.Clt_Un).Code(ctx, VMCodes.Push4).Int(0).Code(ctx, VMCodes.Ceq).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void Blt(int dest) => buffer.Code(ctx, VMCodes.Clt).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void BltUn(int dest) => buffer.Code(ctx, VMCodes.Clt_Un).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void Ble(int dest) => buffer.Code(ctx, VMCodes.Cgt).Code(ctx, VMCodes.Push4).Int(0).Code(ctx, VMCodes.Ceq).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	internal void BleUn(int dest) => buffer.Code(ctx, VMCodes.Cgt_Un).Code(ctx, VMCodes.Push4).Int(0).Code(ctx, VMCodes.Ceq).Code(ctx, VMCodes.Brtrue).LabelOffset(dest);
	#endregion

	internal void LoadNumber(int i) => buffer.Code(ctx, VMCodes.Push4).Int(i);
	internal void LoadNumber(long i) => buffer.Code(ctx, VMCodes.Push8).Long(i);

	#region math
	internal void Add(bool F) 
	{
		if (F)
			buffer.Code(ctx, VMCodes.FAdd);
		else
			buffer.Code(ctx, VMCodes.Add); 
	}
	internal void Sub(bool F) 
	{ 
		if (F)
			buffer.Code(ctx, VMCodes.FSub);
		else
			buffer.Code(ctx, VMCodes.Sub); 
	}
	internal void Add_Ovf() { buffer.Code(ctx, VMCodes.Add_Ovf); }
	internal void Sub_Ovf() { buffer.Code(ctx, VMCodes.Sub_Ovf); }
	internal void Add_Ovf_Un() { buffer.Code(ctx, VMCodes.Add_Ovf_Un); }
	internal void Sub_Ovf_Un() { buffer.Code(ctx, VMCodes.Sub_Ovf_Un); }
	internal void Div(VMTypes f, VMTypes s, bool F) 
	{
		if (F)
		{
			buffer.Code(ctx, VMCodes.FDiv);
		}
		else
		{
			if (GeneralUtils.MathWith(f, s).IsUnsigned())
				buffer.Code(ctx, VMCodes.UDiv);
			else
				buffer.Code(ctx, VMCodes.IDiv);
		}
	}
	internal void Div_Un() { buffer.Code(ctx, VMCodes.UDiv); }
	internal void Mul(VMTypes f, VMTypes s, bool F) 
	{
		if (F)
		{
			buffer.Code(ctx, VMCodes.FMul);
		}
		else
		{
			if (GeneralUtils.MathWith(f, s).IsUnsigned())
				buffer.Code(ctx, VMCodes.UMul);
			else
				buffer.Code(ctx, VMCodes.IMul);
		}
	}
	internal void Mul_Ovf(VMTypes f, VMTypes s)
	{
		if (GeneralUtils.MathWith(f, s).IsUnsigned())
			buffer.Code(ctx, VMCodes.UMul_Ovf);
		else
			buffer.Code(ctx, VMCodes.IMul_Ovf);
	}
	internal void Mul_Ovf_Un() { buffer.Code(ctx, VMCodes.Mul_Ovf_Un); }
	internal void Rem(bool F) 
	{ 
		if (F)
			buffer.Code(ctx, VMCodes.FRem);
		else
			buffer.Code(ctx, VMCodes.Rem); 
	}
	internal void Rem_Un() { buffer.Code(ctx, VMCodes.Rem_Un); }
	#endregion
	#region logic
	internal void Xor() { buffer.Code(ctx, VMCodes.Xor); }
	internal void Or() { buffer.Code(ctx, VMCodes.Or); }
	internal void And() { buffer.Code(ctx, VMCodes.And); }
	internal void Not() { buffer.Code(ctx, VMCodes.Not); }
	internal void Neg() { buffer.Code(ctx, VMCodes.Neg); }
	internal void Shl() { buffer.Code(ctx, VMCodes.Shl); }
	internal void Shr() { buffer.Code(ctx, VMCodes.Shr); }
	internal void Shr_Un() { buffer.Code(ctx, VMCodes.Shr_Un); }
	#endregion

	// TODO: add support for structs
	internal void LoadStaticField(ushort idx) => buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx).Code(ctx, VMCodes.DerefI); 
	internal void LoadStaticFieldRef(ushort idx) => buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx); 
	internal void SetStaticField(ushort idx) => buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx).SwapStack(ctx).Code(ctx, VMCodes.SetI); 

	#region pointers
	internal void DerefI() => buffer.Code(ctx, VMCodes.DerefI);
	internal void DerefI8() => buffer.Code(ctx, VMCodes.DerefI8);
	internal void DerefI4() => buffer.Code(ctx, VMCodes.DerefI4);
	internal void DerefI2() => buffer.Code(ctx, VMCodes.DerefI2);
	internal void DerefI1() => buffer.Code(ctx, VMCodes.DerefI1);

	internal void DerefU4() => buffer.Code(ctx, VMCodes.DerefU4);
	internal void DerefU2() => buffer.Code(ctx, VMCodes.DerefU2);

	internal void SetSized(ushort size) => buffer.Code(ctx, VMCodes.SetSized).Ushort(size);
	internal void SetI() => buffer.Code(ctx, VMCodes.SetI);
	internal void SetI4() => buffer.Code(ctx, VMCodes.SetI4);
	internal void SetI2() => buffer.Code(ctx, VMCodes.SetI2);
	internal void SetI1() => buffer.Code(ctx, VMCodes.SetI1);
	#endregion

	#region arrays
	internal void NewArr(ushort idx)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx).Code(ctx, VMCodes.NewArr); 
		RegisterHandle();
	}

	// *((offset * size) + header) = result; Where header == 8
	internal void StelemI() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(1).Byte(8).Code(ctx, VMCodes.SetI);
	internal void StelemI1() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(1).Byte(1).Code(ctx, VMCodes.SetI1);
	internal void StelemI2() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(1).Byte(2).Code(ctx, VMCodes.SetI2);
	internal void StelemI4() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(1).Byte(4).Code(ctx, VMCodes.SetI4);
	/* internal void StelemI4() // also works, but slower and maybe harder to decode it as (arr[offfet] = number)?
	{
		buffer.Code(ctx, VMCodes.Eat);
		buffer.Code(ctx, VMCodes.Push4).Int(4);
		buffer.Code(ctx, VMCodes.UMul);
		buffer.Code(ctx, VMCodes.Add);
		buffer.Code(ctx, VMCodes.Push4).Int(16);
		buffer.Code(ctx, VMCodes.Add);
		buffer.Code(ctx, VMCodes.Poop);
		buffer.Code(ctx, VMCodes.SetI4);
	} */

	internal void LdelemI() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(0).Byte(8).Code(ctx, VMCodes.DerefI);
	internal void LdelemI1() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(0).Byte(1).Code(ctx, VMCodes.DerefI1);
	internal void LdelemI2() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(0).Byte(2).Code(ctx, VMCodes.DerefI2);
	internal void LdelemI4() => buffer.Code(ctx, VMCodes.PrepareArr).Byte(0).Byte(4).Code(ctx, VMCodes.DerefI4);

	internal void Ldlen() => buffer.Code(ctx, VMCodes.Push4).Int(8).Code(ctx, VMCodes.Add).Code(ctx, VMCodes.DerefI);
	#endregion

	internal void Call(ushort idx, byte argscount, bool ret, byte floatMask = 0)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx);
		Calli(argscount, ret, floatMask);
	}

	internal void CallManaged(ushort idx, MethodSignature sig, bool ret, bool thisCall)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx);
		byte flags = ret ? (byte)1 : (byte)0; // TODO: Add flags random pos
		flags |= (byte)((thisCall ? 1 : 0) << 1);
		if (sig.ReturnsValue && sig.ReturnType.IsValueType)
		{
			flags |= (byte)(1 << 2);
			if (sig.ReturnType.GetImpliedMemoryLayout(false).Size is 1 or 2 or 4 or 8)
				flags |= (byte)(1 << 3);
		}
		//Console.WriteLine(flags);
		buffer.Code(ctx, VMCodes.CallManaged).Ushort((ushort)(sig.ParameterTypes.Count*8)).Byte(flags);
		for(int i = sig.ParameterTypes.Count - 1; i >= 0; i--)
		{
			var arg = sig.ParameterTypes[i];
			if (arg.IsStruct())
			{
				buffer.Byte(1);
				var size = (ushort)(arg.GetImpliedMemoryLayout(false).Size);
				//Console.WriteLine(size);
				buffer.Ushort(size);
			}
			else
				buffer.Byte(0);
		}
	}

	internal void SafeCall(ushort idx)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx).Code(ctx, VMCodes.SafeCall).Byte(ctx.GetSafeCallId((ushort)(idx/8)));
		//ctx.MaxArgs = Math.Max(ctx.MaxArgs, argscount);
	}

	internal void Calli(byte argscount, bool ret, byte floatMask = 0)
	{
		buffer.Code(ctx, VMCodes.CallAddress).Byte(argscount).Byte(floatMask);
		if (!ret)
			Code(VMCodes.Pop);
		else
			RegisterHandle();
		ctx.MaxArgs = Math.Max(ctx.MaxArgs, argscount & sbyte.MaxValue);
	}

	internal void CallVirt(ushort methodIdx, byte argscount, bool ret, byte floatByte = 0)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(methodIdx);
		buffer.Code(ctx, VMCodes.GetVirtFunc).Short((short)((argscount-1)*8));
		Calli(argscount, ret, floatByte);
	}

	internal void CallInterface(ushort idx, bool isconstrained)
	{ // TODO: adds arg for floats
		buffer.Code(ctx, VMCodes.CallInterface).Ushort(idx).Byte(isconstrained ? (byte)1 : (byte)0);
	}

	internal void LoadVMTable(ushort idx) => buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx);

	internal void Ldstr(string str)
	{
		buffer.Code(ctx, VMCodes.Ldstr).Int(str.Length*2 + 2);
		unsafe
		{
			fixed (char* c = str)
				for (int i = 0; i < str.Length; i++)
					buffer.Short((short)c[i]);
		}
		buffer.Short(0); // \0
		//RegisterHandle();
	}

	internal void NewObj(ushort type, ushort constructor, byte args, bool isValueType, uint size, byte floatMask = 0)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(type);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(ctx.Transform(ctx.CreateObject)); 
		Calli(1, true);
		if (isValueType)
		{
			buffer.Code(ctx, VMCodes.Push4).Int(8).Code(ctx, VMCodes.Add);
		}
		buffer.Code(ctx, VMCodes.Eat);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(constructor); 
		//buffer.Code(ctx, CallAdr).Byte((byte)(args ^ 0b1000_0000));
		Calli((byte)(args ^ 0b1000_0000), false, floatMask);
		buffer.Code(ctx, VMCodes.Poop);
		switch(size) // all 2/4/8 bytes sized structs cant be passed as ptr
		{
			case 2: DerefU2(); break;
			case 4: DerefU4(); break;
			case 8: DerefI8(); break;
		}
		ctx.MaxArgs = Math.Max(ctx.MaxArgs, args);
		RegisterHandle();
	}

	internal void NewObjUnsafe(ushort idx)
	{
		buffer.Code(ctx, VMCodes.NewObjUnsafe).Ushort(idx);
		RegisterHandle();
		/*
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(ctx.GetObjStub(size));
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(ctx.Transform(ctx.CreateObject));
		buffer.Code(ctx, CallAdr).Byte(1);
		buffer.Code(ctx, VMCodes.Eat);
		buffer.Code(ctx, VMCodes.Poop);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(typeIdx);
		buffer.Code(ctx, VMCodes.SetI);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(constructor);
		buffer.Code(ctx, CallAdr).Byte((byte)(args ^ 0b1000_0000));
		buffer.Code(ctx, VMCodes.Pop);
		buffer.Code(ctx, VMCodes.Poop);
		ctx.MaxArgs = Math.Max(ctx.MaxArgs, args);
		*/
	}

	// AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
	internal void CreAAAAAAAAAAAAteDelegAAAAAAAAAAAAAte(ushort typeIdx, bool isStatic)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(typeIdx);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(ctx.GetDelegateInternalAlloc());
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(ctx.GetDelegateStaticCtor());
		buffer.Code(ctx, VMCodes.CreateDelegate);
		RegisterHandle();
	}

	internal void ReplaceTypeHandle(ushort idx)
	{
		buffer.Code(ctx, VMCodes.Eat);
		buffer.Code(ctx, VMCodes.Poop);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx);
		buffer.Code(ctx, VMCodes.SetI);
		buffer.Code(ctx, VMCodes.Poop);
	}

	internal void BeginFinTry(CilExceptionHandler handler)
	{
		var finaly = $"anon_{anonid}";
		anonid++;
		anonlabels.Add(handler, finaly);

		buffer.Code(ctx, VMCodes.FinTry).LabelOffset(finaly);
	}

	internal void BeginFinally(CilExceptionHandler handler)
	{
		var label = anonlabels[handler];
		buffer.AppendLine(label + ':');
	}

	internal void EndFinally()
	{
		Code(VMCodes.NoRet);
	}

	internal void BeginTryCatch(CilExceptionHandler handler, byte type)
	{
		var finaly = $"anon_{anonid}";
		anonid++;
		anonlabels.Add(handler, finaly);

		buffer.Code(ctx, VMCodes.TryCatch).Byte(type).LabelOffset(finaly);
	}

	internal void BeginTryCatchHandler(CilExceptionHandler handler)
	{
		var label = anonlabels[handler];
		buffer.AppendLine(label + ':');
	}

	internal void Leave(int dest) => buffer.Code(ctx, VMCodes.Leave).LabelOffset(dest);

	// TODO: add support for structs (replace with cpblk)
	internal void StoreLocal(ushort offset, bool isvaluetype, ushort size)
	{
		buffer.Code(ctx, VMCodes.Store).Ushort(offset);
		buffer.Byte(isvaluetype ? (byte)1 : (byte)0);
		if (isvaluetype)
			buffer.Ushort(size);
	}
	// TODO: add support for structs (copy)
	internal void LoadLocal(ushort offset, bool isStruct)
	{
		if (isStruct) 
			LoadLocalRef(offset);
		else
			buffer.Code(ctx, VMCodes.Load).Ushort(offset);
	}
	internal void LoadLocalRef(ushort offset) => buffer.Code(ctx, VMCodes.LoadRef).Ushort(offset);

	internal void SetField(ushort idx, uint size)
	{
		buffer.SwapStack(ctx);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx);
		Code(VMCodes.Add);
		buffer.SwapStack(ctx);
		switch (size)
		{
			case 1: buffer.Code(ctx, VMCodes.SetI1); break;
			case 2: buffer.Code(ctx, VMCodes.SetI2); break;
			case 4: buffer.Code(ctx, VMCodes.SetI4); break;
			case 8: buffer.Code(ctx, VMCodes.SetI); break;
			default: buffer.Code(ctx, VMCodes.SetSized).Ushort((ushort)size); break;
		}
	}

	internal void LoadFieldRef(ushort idx)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx);
		buffer.Code(ctx, VMCodes.Add);
	}

	internal void LoadField(ushort idx, uint size)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(idx);
		Code(VMCodes.Add);
		switch (size)
		{
			case 1: buffer.Code(ctx, VMCodes.DerefI1); break;
			case 2: buffer.Code(ctx, VMCodes.DerefI2); break;
			case 4: buffer.Code(ctx, VMCodes.DerefI4); break;
			case 8: buffer.Code(ctx, VMCodes.DerefI); break;
			default:
				break; // load structs as ref
		}
	}

	internal void Initobj(uint size)
	{
		buffer.Code(ctx, VMCodes.Push4).Int(0).Code(ctx, VMCodes.Push4).Int((int)size).Code(ctx, VMCodes.Initblk);
	}

	internal void PushBack(ushort offset)
	{
		buffer.Code(ctx, VMCodes.PushBack).Ushort((ushort)(offset * (ushort)8));
	}

	internal void Ret(ushort size)
	{
		if (size is 8 or 4 or 2 or 1) size = 0;
		buffer.Code(ctx, VMCodes.Ret).Ushort(size);
	}

	internal void Conv(VMTypes inType, CilCode code) => buffer.Code(ctx, VMCodes.Conv).Byte((byte)inType).Ushort((ushort)code);

	internal void Box(ushort typeIdx, ushort size)
	{
		if (size < 8) // allign
			size = 8;
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(typeIdx);
		LoadField(ctx._ValueFieldRuntimeType(), 8);
		buffer.Code(ctx, VMCodes.Box).Ushort(size);
	}

	internal void Unbox(ushort size) => buffer.Code(ctx, VMCodes.Unbox).Ushort(size);

	internal void Isinst(ushort to)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(to);
		LoadField(ctx._ValueFieldRuntimeType(), 8);
		buffer.SwapStack(ctx);
		Call(ctx._IsInstanceOfAny(), 2, true);
	}

	internal void IsinstInterface(ushort to)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(to);
		LoadField(ctx._ValueFieldRuntimeType(), 8);
		buffer.SwapStack(ctx);
		Call(ctx._IsInstanceOfInterface(), 2, true);
	}

	internal void Castclass(ushort to)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(to);
		LoadField(ctx._ValueFieldRuntimeType(), 8);
		buffer.SwapStack(ctx);
		Call(ctx._ChkCastClass(), 2, true);
	}

	internal void Castinterface(ushort to)
	{
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(to);
		LoadField(ctx._ValueFieldRuntimeType(), 8);
		buffer.SwapStack(ctx);
		Call(ctx._ChkCastInterface(), 2, true);
	}

	internal void Code(VMCodes code) => buffer.Code(ctx, code);

	private void RegisterHandle()
	{
		return;
		buffer.Code(ctx, VMCodes.Dup);
		buffer.Code(ctx, VMCodes.PushInstanceID);
		buffer.Code(ctx, VMCodes.SwapStack);
		buffer.Code(ctx, VMCodes.Push4).Int(0);
		buffer.Code(ctx, VMCodes.VMTableLoad).Ushort(ctx.Transform(ctx.RCResolver));
		//buffer.Code(ctx, CallAdr).Byte(3);
		Calli(3, false);
	}

	internal byte[] Finish()
	{
#if false
		Console.WriteLine(buffer.ToString());
#endif
		try
		{
			return assembler.Assemble(buffer.ToString());
		}
		catch(FasmException ex)
		{
			Console.WriteLine($"Line: {ex.Line}\nErrorCode: {ex.ErrorCode}\nMnemonics:\n{string.Join('\n', ex.Mnemonics)}");
			throw ex;
		}
	}
}
