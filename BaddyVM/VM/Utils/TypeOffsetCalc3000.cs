using AsmResolver.DotNet;
using AsmResolver.DotNet.Memory;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
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

        if (ctx != null)
		{
			if (resolved.Module == ctx.core.module && resolved.GenericParameters.Count == 0)
			{
				UpTypes(resolved, ctx.core.module.CorLibTypeFactory);
			}
		}

        //resolved.IsSequentialLayout = true;
		dict = new(resolved.Fields.Count);

		uint offset = resolved.IsValueType ? 0u : 8u;
		var list = new List<TypeDefinition>();
		var t = resolved;
		while (t != null)
		{
			list.Insert(0, t);
			t = t.BaseType?.Resolve();
		}

		foreach(var tt in list)
		{
			foreach (var field in tt.Fields)
			{
				dict.Add(field, offset);
				var size = field.Signature.FieldType.GetImpliedMemoryLayout(true).Size;
				offset += size;
			}
		}

		map.Add(type, dict);

		return dict[fd];
	}

	private static Dictionary<ITypeDefOrRef, uint> SizeCache = new(16);

	internal static uint GetSize(this ITypeDefOrRef type)
	{
		if (SizeCache.TryGetValue(type, out var result))
			return result;
		var t = type.Resolve();
		uint offset = 0u;
		while (t != null)
		{
			foreach (var field in t.Fields)
			{
				var size = field.Signature.FieldType.GetImpliedMemoryLayout(true).Size;
				offset += size;
			}
			t = t.BaseType?.Resolve();
		}
		SizeCache.Add(type, offset);
		return offset;
	}

	private static void UpTypes(TypeDefinition td, CorLibTypeFactory factory)
	{
		foreach(var f in td.Fields)
		{
			var size = f.Signature.FieldType.GetImpliedMemoryLayout(false).Size;
			if (size <= 8)
				f.Signature = new AsmResolver.DotNet.Signatures.FieldSignature(factory.Int64);
		}
	}
}
