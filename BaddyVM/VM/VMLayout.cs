using System;

namespace BaddyVM.VM;
internal class VMLayout
{
	internal int VMHeaderEnd	= 8 * 6;
	internal int LocalStackHeap = 8 * 0; // long*
	internal int LocalStorage	= 8 * 1; // long
	internal int VMTable		= 8 * 2; // long*
	internal int JMPBack		= 8 * 3; // method pointer
	internal int GlobalHeap		= 8 * 4; // long* ???
	internal int MethodFlags	= 8 * 5; // bit long

	internal int MethodNoRet		= 0b0001;

	internal void Randomize()
	{
		// TODO: implement
	}
}
