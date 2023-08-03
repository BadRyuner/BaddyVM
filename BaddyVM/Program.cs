using AsmResolver.DotNet;
using BaddyVM.VM;

namespace BaddyVM;

internal class Program
{
	internal static bool SKIP_STRINGS_INIT_VTABLE = false;

	static void Main(string[] args) // rewrite it for own purposes, atm it in development mode
	{
		string save = null;
		string target = null;
		var what = 2;
		string net = default;
#if NET7_0
		net = "7.0";
#endif
#if NET6_0
		net = "6.0";
#endif
		if (what == 0)
		{
			target = $"D:\\Work\\BaddyVM\\Crackme\\bin\\Debug\\net{net}\\Crackme.dll"; 
			save = "D:\\Test\\Crackme.dll";
		}
		else if (what == 1)
        {
			target = $"D:\\Work\\BaddyVM\\WinFormsApp1\\bin\\Debug\\net{net}-windows\\WinFormsApp1.dll";
			save = "D:\\Test\\WinFormsApp1.dll";
			BaddyVM.VM.Protections.GeneralProtections.DisableForTypes = true; // incompitable with winforms
		}
		else
		{
			SKIP_STRINGS_INIT_VTABLE = true; // idk why
			target = $"D:\\Work\\BaddyVM\\WpfApp1\\bin\\Release\\net{net}-windows\\WpfApp1.dll";
			save = "D:\\Test\\WpfApp1.dll";
		}
		var vm = new VMCore(target, applyProtections: true);
		var methods = vm.module.GetAllTypes().SelectMany(t => t.Methods).Where(m => /*and this*/ 
			//m.IsPublic &&
			!m.IsConstructor 
			&& !m.CustomAttributes.Any(a => a.Constructor.DeclaringType.Name == "GeneratedCodeAttribute")
			&&
			m.CilMethodBody != null);
#if DEBUG
		BaddyVM.VM.Protections.AntiDebug.ForceDisable = true;
#endif
		vm.Virtualize(methods);
		vm.Save(save);
	}

	static bool IsStaticConstructor(AsmResolver.DotNet.MethodDefinition md) => md.IsConstructor ? !md.IsStatic : true;
}
