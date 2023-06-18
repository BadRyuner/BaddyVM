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
}
