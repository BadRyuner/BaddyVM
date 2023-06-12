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
internal static class AntiGenericStruct3000
{
	private static Dictionary<int, TypeSignature> cache = new(4);

	internal static TypeSignature Fix(this GenericInstanceTypeSignature gits, VMContext ctx)
	{
		if (!gits.IsValueType)
		{
			if (cache.TryGetValue(8, out var r))
				return r;
			var r8 = new TypeDefinition(null, $"struct{8}", TypeAttributes.Public, ctx.core.module.CorLibTypeFactory.IntPtr.Resolve().BaseType.ImportWith(ctx.core.module.DefaultImporter));
			ctx.core.module.TopLevelTypes.Add(r8);
			r8.ClassLayout = new ClassLayout(0, 8);
			cache.Add(8, r8.ToTypeSignature());
			return r8.ToTypeSignature();
		}

		var size = gits.Resolve().Fields.Count * 8;

		if (cache.TryGetValue(size, out var result))
			return result;

		var newtype = new TypeDefinition(null, $"struct{size}", TypeAttributes.Public, ctx.core.module.CorLibTypeFactory.IntPtr.Resolve().BaseType.ImportWith(ctx.core.module.DefaultImporter));
		ctx.core.module.TopLevelTypes.Add(newtype);
		newtype.ClassLayout = new ClassLayout(0, (uint)(size));
		cache.Add(size, newtype.ToTypeSignature());
		return newtype.ToTypeSignature();
	}
}
