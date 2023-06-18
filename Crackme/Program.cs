namespace Crackme;

public class Program
{
	public void Print()
	{
		
	}

	private static void Main(string[] args)
	{
		float f = 4365753573.376536f;
		float ff = -4365753573.376536f;
		Console.WriteLine((byte)f);
		Console.WriteLine((sbyte)ff);
		Console.WriteLine((ushort)f);
		Console.WriteLine((short)ff);
		Console.WriteLine((uint)f);
		Console.WriteLine((int)ff);
		Console.WriteLine((ulong)f);
		Console.WriteLine((long)ff);
		Console.WriteLine((float)f);
		Console.WriteLine((float)ff);
		Console.WriteLine((double)f);
		Console.WriteLine((double)ff);
		Console.ReadLine();
	}
}