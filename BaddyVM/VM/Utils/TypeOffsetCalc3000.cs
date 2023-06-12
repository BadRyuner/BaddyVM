using AsmResolver.DotNet;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Signatures.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaddyVM.VM.Utils;
internal static class TypeOffsetCalc3000
{
	private static Dictionary<ITypeDefOrRef, Dictionary<IFieldDescriptor, uint>> map = new(16);

	internal static uint GetOffset(this VMContext ctx, ITypeDefOrRef type, IFieldDescriptor fd)
	{
		if (map.TryGetValue(type, out var dict))
			return dict[fd];

		var resolved = type.Resolve();

        if (resolved.Module == ctx.core.module)
        {
            UpTypes(resolved, ctx.core.module.CorLibTypeFactory);
        }

        resolved.IsSequentialLayout = true;
		dict = new(resolved.Fields.Count);

		uint offset = resolved.IsValueType ? 0u : 8u;
		foreach(var field in resolved.Fields)
		{
			dict.Add(field, offset);
			var size = field.Signature.FieldType.GetImpliedMemoryLayout(true).Size;
			offset += size;
		}

		map.Add(type, dict);

		return dict[fd];
	}

	private static void UpTypes(TypeDefinition td, CorLibTypeFactory factory)
	{
		foreach(var f in td.Fields)
		{
			var size = f.Signature.FieldType.GetImpliedMemoryLayout(false).Size;
			if (size < 8)
				f.Signature = new AsmResolver.DotNet.Signatures.FieldSignature(factory.Int64);
		}
	}
}
