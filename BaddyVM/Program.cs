using BaddyVM.VM;

namespace BaddyVM;

internal class Program
{
	static void Main(string[] args)
	{
		var target = "D:\\Work\\BaddyVM\\Crackme\\bin\\Debug\\net6.0\\Crackme.dll"; // change this 
		var vm = new VMCore(target);
		var methods = vm.module.GetAllTypes().SelectMany(t => t.Methods).Where(m => /*and this*/ 
			//!m.IsPublic && 
			m.CilMethodBody != null);
		vm.Virtualize(methods);
		vm.Save("D:\\Test\\Crackme.dll");
	}
}
