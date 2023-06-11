using AsmResolver.DotNet;
using AsmResolver.DotNet.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaddyVM.VM.Utils;
internal static class TypeOffsetCalc3000
{
	private static Dictionary<ITypeDefOrRef, Dictionary<IFieldDescriptor, uint>> map = new(16);

	internal static uint Get(ITypeDefOrRef type, IFieldDescriptor fd)
	{
		if (map.TryGetValue(type, out var dict))
			return dict[fd];

		var resolved = type.Resolve();
		resolved.IsSequentialLayout = true;
		dict = new(resolved.Fields.Count);

		uint offset = resolved.IsValueType ? 0u : 8u;
		foreach(var field in resolved.Fields)
		{
			dict.Add(field, offset);
			offset += field.Signature.FieldType.GetImpliedMemoryLayout(true).Size;
		}

		map.Add(type, dict);

		return dict[fd];
	}
}
