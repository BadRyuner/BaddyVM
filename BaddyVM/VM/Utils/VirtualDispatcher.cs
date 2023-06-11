using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BaddyVM.VM.Utils;
internal static class VirtualDispatcher
{
	private static Dictionary<TypeDefinition, MethodChunks> Map = new(32);

	internal static (ushort chunk, ushort offset) GetOffset(MethodDefinition md)
	{
		if (Map.TryGetValue(md.DeclaringType, out var m))
			return m.Resolve(md);
		
		Map.Add(md.DeclaringType, MethodChunks.RecursiveFill(md.DeclaringType));
		return Map[md.DeclaringType].Resolve(md);
	}

	static SignatureComparer comparer = new();

	struct MethodChunks
	{
		internal struct MethodChunk
		{
			public MethodChunk() { }

			internal Dictionary<MethodDefinition, ushort> vtable = new(8);

			internal bool CanFit(ushort offset) => offset <= 0x38;

			internal bool ContainsOverride(MethodDefinition md) => vtable.Keys.Any(k => k.Name == md.Name && comparer.Equals(k.Signature, md.Signature));

			internal void FitOverride(MethodDefinition md) =>
				Fit(vtable.First(k => k.Key.Name == md.Name && comparer.Equals(k.Key.Signature, md.Signature)).Value, md);

			internal void Fit(ushort offset, MethodDefinition md) => vtable.Add(md, offset);
		}

		public MethodChunks() { }

		internal Dictionary<ushort, MethodChunk> chunks = new(2);

		internal static MethodChunks RecursiveFill(TypeDefinition type)
		{
			if (Map.TryGetValue(type, out var m))
				return m;

			var current = new MethodChunks();

			if (type.BaseType != null)
			{
				var parent = RecursiveFill(type.BaseType.Resolve());
				foreach(var i in parent.chunks)
					current.chunks.Add(i.Key, i.Value);
			}

			ushort chunk = current.chunks.Keys.Count == 0 ? (ushort)0 : (ushort)current.chunks.Keys.Max();
			ushort offset = current.chunks.Keys.Count == 0 ? (ushort)0 : 
				current.chunks[chunk].vtable.Values.Count == 0 ? (ushort)0 : (ushort)(current.chunks[chunk].vtable.Values.Max() + 8);

			if (current.chunks.Count == 0)
				current.chunks.Add(chunk, new MethodChunk());

			foreach(var method in type.Methods.Where(m => m.IsVirtual))
			{
				foreach(var c in current.chunks.Values)
					if (c.ContainsOverride(method))
					{
						c.FitOverride(method);
						goto end;
					}

				if (offset > 0x38)
				{
					chunk++;
					offset = 0;
					current.chunks.Add(chunk, new MethodChunk());
				}

				current.chunks[chunk].Fit(offset, method);

				offset += 8;
				end: continue;
			}

			/*
			ushort offset = dict.Values.Count == 0 ? (ushort)0 : (ushort)(dict.Values.Max() + 8);

			foreach (var m in type.Methods.Where(m => m.IsVirtual))
			{
				foreach (var old in dict)
				{
					if (m.Name == old.Key.Name && comparer.Equals(m.Signature, old.Key.Signature))
					{
						dict.Add(m, old.Value);
						goto end;
					}
				}
				dict.Add(m, offset);
				offset += 8;
				end: continue;
			} */
			return current;
		}

		internal (ushort, ushort) Resolve(MethodDefinition md)
		{
			foreach(var c in chunks)
				foreach(var p in c.Value.vtable)
					if (p.Key == md)
						return (c.Key, p.Value);
			throw new NotSupportedException();
		}
	}
}
