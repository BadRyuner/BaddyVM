using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;

namespace BaddyVM.VM.Protections;
internal class GeneralProtections
{
	internal static bool DisableForTypes = false;

	internal static void Protect(VMContext ctx, CilMethodBody body)
	{
		General.Recontrol.Apply(ctx, body);
		General.NativeOp .Apply(ctx, body);
		General.ConstMelt.Apply(ctx, body);
	}

	internal static void ProtectType(VMContext ctx, TypeDefinition type)
	{
		if (DisableForTypes) return;
		if (type.IsModuleType) return;

		var forceRename = type.IsNotPublic || type.IsNestedPrivate;
		if (forceRename)
		{
			//type.Namespace = null;
			//type.Name = newName;
		}

		foreach (var f in type.Fields)
		{
			if (!f.IsPublic || forceRename)
				f.Name = newName;
			
			//if (!f.Signature.FieldType.IsValueType)
			//	f.Signature.FieldType = ctx.core.module.CorLibTypeFactory.Object;
		}
	}

	private static Utf8String newName = "a";
}
