using System;

namespace BaddyVM.VM;
internal enum VMCodes : byte
{
	None = 0,
	Push4, // TODO: Add 1, 2
	Push8,
	Ldstr,
	
	Br, Brtrue, Brfalse, Switch,

	Ceq, Clt, Clt_Un, Cgt, Cgt_Un,

	Add, Sub, 
	FAdd, FSub, FMul, FDiv, FRem,
	IMul, UMul,
	IDiv, UDiv, 
	Rem,

	Add_Ovf, Sub_Ovf,
	IMul_Ovf, UMul_Ovf,

	Add_Ovf_Un, Sub_Ovf_Un,
	Mul_Ovf_Un,
	Rem_Un,

	Xor, Or, Not, Neg, And, Shl, Shr, Shr_Un,

	DerefI, DerefI8, DerefI4, DerefI2, DerefI1, // DerefR4, DerefR8,
	SetI, SetI4, SetI2, SetI1, SetSized,

	Conv,

	Box, Unbox,

	NewArr, PrepareArr,

	NewObjUnsafe, // unsafe because can be easy RE

	CreateDelegate,

	Eat, Poop, PoopRef,

	Dup, Pop,

	VMTableLoad,

	GetVirtFunc,

	CallAddress,
	SafeCall, // just for structs, im stupid for naming
	CallInterface,
	Jmp,

	SwapStack,

	Store, Load, LoadRef,

	TryCatch,
	FinTry, Leave, NoRet,

	Initblk, PushBack,

	Ret,

	Intrinsic, // TODO, Add intrinsics like Console.WriteLine -> asm call to sys
			   // or direct write to buffer via field in Console class

	PatchAtRuntime, // TODO, replace VMTableLoad with Push8, https://github.com/Washi1337/AsmResolver/issues/444

	Max
}
