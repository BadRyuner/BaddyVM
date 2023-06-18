using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using System;
using System.Collections.Generic;

namespace BaddyVM.VM.Protections;
internal class GeneralProtections
{
	internal static void Protect(VMContext ctx, CilMethodBody body)
	{
		General.ConstMelt.Apply(ctx, body);
	}

	internal static void ProtectType(VMContext ctx, TypeDefinition type)
	{
		Utf8String newName = "a";
		if (!type.IsPublic && !type.IsModuleType)
		{
			type.Namespace = null;
			type.Name = newName;
		}

		foreach (var f in type.Fields)
		{
			if (!f.IsPublic || !type.IsPublic)
				f.Name = newName;
			
			if (!f.Signature.FieldType.IsValueType)
				f.Signature.FieldType = ctx.core.module.CorLibTypeFactory.Object;
		}
	}
}
