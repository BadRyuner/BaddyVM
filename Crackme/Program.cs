namespace Crackme;

public unsafe class Program
{
	public Program(int a, int b, int c)
	{
		Console.WriteLine($"{a} - {b} - {c}");
		Console.WriteLine(this.GetType());
	}

	public int aboba;
	public long kek;

	public Program()
	{
		aboba = 123;
		kek = 456;
	}

	private static void Main(string[] args)
	{
		//var r = DoSomething();
		//Console.WriteLine(r == null);
		//Test(r);
		//Test1(20, 40);
		//foreach(var i in new int[] { 1,2,3,4,5,6,7,8,9,10 }) // no crash
		//	Console.WriteLine(i);

		var list = Test();
		Console.WriteLine(list.Count);
		foreach(var i in list)
			Console.WriteLine(i);
	}

	public static List<int> Test()
	{
		return new List<int>() { 1, 2, 3, 4, 5, 6, 7 };
	}
}
