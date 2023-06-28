﻿using BaddyVM.VM;

namespace BaddyVM;

internal class Program
{
	static void Main(string[] args) // rewrite it for own purposes, atm it in development mode
	{
		string save = null;
		string target = null;
		var what = 0;
		if (what == 0)
		{
			target = "D:\\Work\\BaddyVM\\Crackme\\bin\\Debug\\net6.0\\Crackme.dll"; 
			save = "D:\\Test\\Crackme.dll";
		}
		else if (what == 1)
        {
			target = "D:\\Work\\BaddyVM\\WinFormsApp1\\bin\\Debug\\net6.0-windows\\WinFormsApp1.dll";
			save = "D:\\Test\\WinFormsApp1.dll";
			BaddyVM.VM.Protections.GeneralProtections.DisableForTypes = true; // incompitable with winforms
		}
		else
		{
			target = "D:\\Work\\BaddyVM\\WpfApp1\\bin\\Debug\\net6.0-windows\\WpfApp1.dll";
			save = "D:\\Test\\WpfApp1.dll";
		}
		var vm = new VMCore(target, applyProtections: true);
		var methods = vm.module.GetAllTypes().SelectMany(t => t.Methods).Where(m => /*and this*/ 
			//m.IsPublic &&
			//!m.IsConstructor &&
			IsStaticConstructor(m) &&
			m.CilMethodBody != null);
#if DEBUG
		BaddyVM.VM.Protections.AntiDebug.ForceDisable = true;
#endif
		vm.Virtualize(methods);
		vm.Save(save);
	}

	static bool IsStaticConstructor(AsmResolver.DotNet.MethodDefinition md) => md.IsConstructor ? !md.IsStatic : true;
}
