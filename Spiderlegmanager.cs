using Godot;
using System.Collections.Generic;


/*
Attach this to the parent node of a group of legs (e.g. the spider body
root). Provides a single button that walks every descendant, finds every
Fabriklegchain, and forces each one to reinitialize - equivalent to
duplicating each leg node, but without the click-per-leg tedium.
*/

[Tool]
[GlobalClass]
public partial class Spiderlegmanager : Node3D
{
	[ExportToolButton("Reinitialize all legs")]
	public Callable ReinitializeAllLegsButton => Callable.From(ReinitializeAllLegs);

	public void ReinitializeAllLegs()
	{
		List<Fabriklegchain> legs = new List<Fabriklegchain>();
		CollectLegs(this, legs);

		foreach (Fabriklegchain leg in legs)
			leg.ForceResolve();

		GD.Print($"SpiderLegManager: reinitialized {legs.Count} leg(s).");
	}

	// Recursively walks every descendant looking for Fabriklegchain nodes,
	// regardless of how deeply nested they are under this node.
	private static void CollectLegs(Node node, List<Fabriklegchain> results)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Fabriklegchain leg)
				results.Add(leg);

			CollectLegs(child, results);
		}
	}
}
