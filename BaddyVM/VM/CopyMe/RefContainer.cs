using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace BaddyVM.VM.CopyMe;
internal static class RefContainer
{
	static Dictionary<long, List<IntPtr>> container = new(4); // in theory should protect objects from GC :(

	static void Resolve(long l, object b, byte id)
	{
		return;
		switch(id)
		{
			case 0:
				if (b == null)
					return;
				Get(l).Add((IntPtr)GCHandle.Alloc(b));
				break;
			case 1:
				if (container.ContainsKey(l))
				{
					foreach(var x in container[l])
						GCHandle.FromIntPtr(x).Free();
					container[l].Clear();
					container.Remove(l);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(id));
		}
	}

	static List<IntPtr> Get(long l)
	{
		if (container.TryGetValue(l, out var result))
			return result;
		result = new(4);
		container.Add(l, result);
		return result;
	}

	static long Next()
	{
		if (container.Count == 0)
			return 1;
		return container.Keys.Max() + 1;
	}
}
