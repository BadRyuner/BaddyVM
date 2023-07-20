using AsmResolver.DotNet;

namespace BaddyVM.VM.Intrinsics;
internal static partial class IntrinsicsFactory
{
	private static Dictionary<IMethodDescriptor, MethodDefinition> cache = new(16);
	private static List<IMethodDescriptor> skipThem = new(16);
	private static List<Func<IMethodDescriptor, MethodDefinition>> funcs = new(16);
	private static VMContext ctx;

	static IntrinsicsFactory()
	{
		//FastAndUnsafeEmptyArray.Register();
	}

	internal static void Init(VMContext context) => ctx = context; 

	internal static MethodDefinition Get(IMethodDescriptor target)
	{
		if (cache.TryGetValue(target, out var result))
			return result;

		if (skipThem.Contains(target)) return null;
		
		for(var i = 0; i < funcs.Count; i++)
		{
			result = funcs[i](target);
			if (result != null) 
				return result;
		}

		skipThem.Add(target);

		return null;
	}
}
