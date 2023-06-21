using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Marshal;
using AsmResolver.PE.DotNet.Cil;
using BaddyVM.VM.Utils;
using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaddyVM.VM.Protections;
internal static class ProtectLinks
{
	internal static void Protect(VMCore core)
	{
		return; // The dll is successfully loaded by Assembly.Load(byte[]),
				// but .net wants to load it from disk instead of using the loaded one.
				// Idk how to bypass it.

		var ctx = core.context;
		var mod = core.module;
		var dlls = new FileInfo(core.path).Directory.EnumerateFiles("*.dll")
			.Select(s =>
			{
				try { return (s, System.Reflection.AssemblyName.GetAssemblyName(s.FullName).Name); } 
				catch { return (null, null); }
			}).Where(s => s.Item2 != null).ToArray();
		var inFolder = mod.AssemblyReferences.Where(asm => dlls.Any(s => s.Name == asm.Name)).ToArray();
		var loadAsm = mod.DefaultImporter.ImportMethod(typeof(System.Reflection.Assembly).GetMethod("Load", new[] { typeof(byte[]) }));
		var sig = MethodSignature.CreateStatic(mod.CorLibTypeFactory.Void, ctx.PTR);
		foreach (var asm in inFolder)
		{
			var dll = dlls.First(s => s.Name == asm.Name).s.FullName;
			var bytes = File.ReadAllBytes(dll);
			var writer = HighLevelIced.Get(ctx);

			var start = writer.AddData(0); // must be mounted to Byte[].TypeHandle, but AsmResolver has not been updated yet(( In anyway this works lol
			writer.AddData(bytes.Length);
			writer.AddData(bytes);
			writer.asm.mov(writer.Rax, writer.Arg1_64);
			writer.asm.lea(writer.Arg1_64, AssemblerRegisters.__[start]);
			writer.JmpReg(writer.Rax);

			var proxyContent = writer.Compile();

			var proxy = ctx.AllocNativeMethod(asm.Name, sig);
			proxy.Code = proxyContent;
			var cctor = mod.GetOrCreateModuleConstructor();
			var instructions = cctor.CilMethodBody.Instructions;
			instructions.Insert(0, new CilInstruction(CilOpCodes.Ldftn, loadAsm));
			instructions.Insert(1, new CilInstruction(CilOpCodes.Call, proxy.Owner));
		}
	}
}
