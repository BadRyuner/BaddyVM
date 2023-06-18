using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using System;
using System.Collections.Generic;

namespace BaddyVM.VM.Protections.General;
internal class ConstMelt
{
	internal static Dictionary<long, MethodDefinition> Cache = new Dictionary<long, MethodDefinition>();

	internal static void Apply(VMContext ctx, CilMethodBody body)
	{
		for(int x = 0; x < body.Instructions.Count; x++)
		{
			var i = body.Instructions[x];
			switch (i.OpCode.Code)
			{
				case CilCode.Ldc_I4_M1:
				case CilCode.Ldc_I4_0:
				case CilCode.Ldc_I4_1:
				case CilCode.Ldc_I4_2:
				case CilCode.Ldc_I4_3:
				case CilCode.Ldc_I4_4:
				case CilCode.Ldc_I4_5:
				case CilCode.Ldc_I4_6:
				case CilCode.Ldc_I4_7:
				case CilCode.Ldc_I4_8:
				case CilCode.Ldc_I4_S:
				case CilCode.Ldc_I4:
					{
						var value = i.GetLdcI4Constant();
						var proxy = GetProxy(ctx, value);
						i.OpCode = CilOpCodes.Call;
						i.Operand = proxy;
						break;
					}
				case CilCode.Ldc_I8:
					{
						var value = (long)i.Operand;
						var proxy = GetProxy(ctx, value);
						i.OpCode = CilOpCodes.Call;
						i.Operand = proxy;
						break;
					}
				default:
					break;
			}
		}
	}

	private static MethodDefinition GetProxy(VMContext ctx, long l)
	{
		if (Cache.TryGetValue(l, out var proxy)) return proxy;
		proxy = ctx.AllocNativeMethod(l.ToString(), MethodSignature.CreateStatic(ctx.PTR)).Owner;
		var asm = HighLevelIced.Get(ctx);
		var data = asm.AddData(l);
		asm.MoveLongToResult(data);
		asm.Return();
		proxy.NativeMethodBody.Code = asm.Compile();
		Cache.Add(l, proxy);
		return proxy;
	}
}
