using System;

namespace BaddyVM.VM;
internal enum VMTypes : byte
{
	None = 0,
	I1 = 0b0000_0001, 
	I2 = 0b0000_0010, 
	I4 = 0b0000_0100, 
	I8 = 0b0000_1000,
	U1 = 0b0001_0001,
	U2 = 0b0001_0010,
	U4 = 0b0001_0100,
	U8 = 0b0001_1000,
	R4 = 0b0010_0000, 
	R8 = 0b0010_0001,
	STR,
	PTR,
	PTR_PTR,
	MAX
}
