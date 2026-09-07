using Godot;
using System;
using System.Linq;
using Godot.Collections;
using System.ComponentModel.DataAnnotations;

public partial class SpiderGait : Node3D
{
	[Export] public Node3D[] legs =  [];
	private Godot.Collections.Array<Node3D> qeue = [];
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		foreach (Node3D leg in legs)
		{
			if ((bool)leg.Get("footMoving"))
			{
				if (!qeue.Contains(leg))
				{
					GD.Print("added leg");
					qeue.Add(leg);
				}
			}
			else
			{
				if (qeue.Contains(leg))
				{
					GD.Print("removed leg");
					qeue.Remove(leg);
				}
			}
		}
		GD.Print(qeue.ToString());
		if(qeue.Count != 0)
		{
			Node3D topLevel = qeue[0];
			topLevel.Set("amAllowed", true);
		}
	}
}
