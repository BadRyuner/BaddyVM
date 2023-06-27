using BaddyVM.VM;

namespace BaddyVM;

internal class Program
{
	static void Main(string[] args)
	{
		var target = "D:\\Work\\BaddyVM\\Crackme\\bin\\Debug\\net6.0\\Crackme.dll"; // change this 
		var vm = new VMCore(target, applyProtections: true);
		var methods = vm.module.GetAllTypes().SelectMany(t => t.Methods).Where(m => /*and this*/ 
			//m.IsPublic && 
			IsStaticConstructor(m) &&
			m.CilMethodBody != null);
#if DEBUG
		BaddyVM.VM.Protections.AntiDebug.ForceDisable = true;
#endif
		vm.Virtualize(methods);
		vm.Save("D:\\Test\\Crackme.dll");
	}

	static bool IsStaticConstructor(AsmResolver.DotNet.MethodDefinition md) => md.IsConstructor ? !md.IsStatic : true;
}
