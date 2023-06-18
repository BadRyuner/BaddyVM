using Terminal.Gui;

namespace Crackme;

public class Program
{
	private static void Main(string[] args)
	{
		AnotherTest();
		//Start();
	}

	static void AnotherTest()
	{
		try
		{
			var list = new List<string>() { "aa", "bb" };
			foreach (var item in list)
			{
				list[0] = "2";
			}
		}
		catch(Exception e)
		{
			Console.WriteLine("CATCHED");
		}
		Console.ReadKey();
	}

	public static void Start()
	{
		Application.Init();
		var window = new Window("Crackme") { X = Pos.Center(), Y = Pos.Center(), Width = 42, Height = 10 };
		window.ColorScheme.Normal = new Terminal.Gui.Attribute(Color.White, Color.Black);

		Label p = null;
		var labels = new Label[8];
		for (int i = 0; i < 8; i++)
		{
			if (p == null)
				p = new Label("0") { X = 1, Y = 3, TextAlignment = TextAlignment.Centered, Width = 3 };
			else
				p = new Label("0") { X = Pos.Right(p) + 2, Y = 3, TextAlignment = TextAlignment.Centered, Width = 3 };
			p.Border = new Border() { BorderStyle = BorderStyle.Rounded, BorderBrush = Color.White, Background = Color.Black };
			labels[i] = p;
			window.Add(p);
			var iCopy = i;
			var up = new Button("\u2191") { X = p.X - 1, Y = 1 };
			up.Border = new Border() { BorderBrush = Color.White, Background = Color.Black };
			up.Clicked += () =>
			{
				var current = int.Parse(labels[iCopy].Text.ToString());
				if (current <= 8)
					labels[iCopy].Text = ((char)(current + 1 + '0')).ToString();
				else
					labels[iCopy].Text = "0";
			};
			var down = new Button("\u2193") { X = p.X - 1, Y = 5 };
			down.Border = new Border() { BorderBrush = Color.White, Background = Color.Black };
			down.Clicked += () =>
			{
				var current = int.Parse(labels[iCopy].Text.ToString());
				if (current > 0)
					labels[iCopy].Text = ((char)(current - 1 + '0')).ToString();
				else
					labels[iCopy].Text = "9";
			};
			window.Add(up, down);
		}

		var button = new Button("Open") { X = Pos.Center(), Y = 7 };
		button.Clicked += () =>
		{
			bool good = false;
			var p1 = long.Parse(labels[0].Text.ToString());
			var p2 = long.Parse(labels[1].Text.ToString());
			long pin = p1;
			pin |= p2 << 8;
			if (pin != 0b0000_0011_0000_0110)
				goto ohno;
			pin |= long.Parse(labels[2].Text.ToString()) << 16;
			pin |= long.Parse(labels[3].Text.ToString()) << 24;
			pin |= long.Parse(labels[4].Text.ToString()) << 32;
			pin |= long.Parse(labels[5].Text.ToString()) << 40;
			pin |= long.Parse(labels[6].Text.ToString()) << 48;
			pin |= long.Parse(labels[7].Text.ToString()) << 56;

			good = pin == 285881730728710;
		ohno:
			if (good)
				MessageBox.Query("Mission Completed!", ":)");
			else
				MessageBox.Query("Bad Pin!", "Try Again! (Press ESC)");
		};

		window.Add(button);
		Application.Top.Add(window);
		Application.Run();
		Application.Shutdown();
	}
}