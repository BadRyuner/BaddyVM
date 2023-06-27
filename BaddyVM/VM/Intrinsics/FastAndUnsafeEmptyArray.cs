using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using BaddyVM.VM.Utils;

namespace BaddyVM.VM.Intrinsics;
internal static partial class IntrinsicsFactory
{
	private static class FastAndUnsafeEmptyArray
	{
		private static MethodDefinition cached;
		private static Utf8String fname = "Empty";
		private static string cname = "System.Array";
		private static MethodDefinition Get(IMethodDescriptor target)
		{
			if (target.Name == fname 
				&& target.DeclaringType.FullName == cname)
			{
				if (cached == null)
				{
					var b = ctx.AllocNativeMethod("FastArray", MethodSignature.CreateStatic(ctx.PTR));
					var w = HighLevelIced.Get(ctx);
					var array = w.AddData(new byte[16]);
					w.MovePtrToResult(array);
					w.Return();
					b.Code = w.Compile();
					cached = b.Owner;
				}
				return cached;
			}
			return null;
		}

		internal static void Register()
		{
			funcs.Add(Get);
		}
	}
}
