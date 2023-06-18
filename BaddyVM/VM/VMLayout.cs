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

	internal int MethodNoRet	= 0b0001;

	internal void Randomize()
	{
		var rand = Random.Shared;
		var offsets = new int[] { LocalStackHeap, LocalStorage, VMTable, JMPBack, GlobalHeap, MethodFlags }.OrderBy(x => rand.Next()).ToArray();
		LocalStackHeap = offsets[0];
		LocalStorage = offsets[1];
		VMTable = offsets[2];
		JMPBack = offsets[3];
		GlobalHeap = offsets[4];
		MethodFlags = offsets[5];

		offsets = new int[] { 0b1, 0b10, 0b100, 0b1000, 0b1_0000, 0b10_0000, 0b100_0000 }.OrderBy(x => rand.Next()).ToArray();
		MethodNoRet = offsets[0];
	}
}
