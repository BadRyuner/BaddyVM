using System;

namespace BaddyVM.VM;
internal enum VMCodes : byte
{
	None = 0,
	Push4, // TODO: Add 1, 2
	Push8,
	Ldstr,
	
	Br, Brtrue, Brfalse,

	Ceq, Clt, Clt_Un, Cgt, Cgt_Un,

	Add, Sub, 
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
	SetI, SetI4, SetI2, SetI1,

	Conv_I, Conv_I8, Conv_I4, Conv_I2, Conv_I1, Conv_U8, Conv_U4, Conv_U2, Conv_U1,
	Conv_Ovf_I, Conv_Ovf_I8, Conv_Ovf_I4, Conv_Ovf_I2, Conv_Ovf_I1, Conv_Ovf_U8, Conv_Ovf_U4, Conv_Ovf_U2, Conv_Ovf_U1,
	Conv_Ovf_I_Un, Conv_Ovf_I8_Un, Conv_Ovf_I4_Un, Conv_Ovf_I2_Un, Conv_Ovf_I1_Un, Conv_Ovf_U8_Un, Conv_Ovf_U4_Un, Conv_Ovf_U2_Un, Conv_Ovf_U1_Un,
	Conv_R_Un, Conv_R4, Conv_R8, // oh shi~

	NewArr, PrepareArr,

	NewObj,

	Eat, Poop,

	Dup, Pop,

	VMTableLoad,

	GetVirtFunc,

	CallAddress,
	SafeCall, // just for structs, im stupid for naming
	CallInterface,
	Jmp, // inimplemented

	SwapStack,

	Store, Load, LoadRef,

	FinTry, Leave, NoRet,

	Initblk, PushBack,

	Ret,

	Intrinsic, // TODO, Add intrinsics like Console.WriteLine -> asm call to sys
			   // or direct write to buffer via field in Console class

	PatchAtRuntime, // TODO, replace VMTableLoad with Push8, https://github.com/Washi1337/AsmResolver/issues/444

	Max
}
