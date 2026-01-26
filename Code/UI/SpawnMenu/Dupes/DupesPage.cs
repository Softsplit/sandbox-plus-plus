using Sandbox.UI;

/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Dupes" ), Order( 3000 ), Icon( "✌️" )]
public class DupesPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddHeader( "Workshop" );
		AddOption( "🎖️", "Popular Dupes", () => new DupesWorkshop() { SortOrder = Storage.SortOrder.RankedByVote } );
		AddOption( "🐣", "Newest Dupes", () => new DupesWorkshop() { SortOrder = Storage.SortOrder.RankedByPublicationDate } );

		AddHeader( "Categories" );

		foreach ( var entry in TypeLibrary.GetEnumDescription( typeof( DupeCategory ) ) )
		{
			AddOption( entry.Icon, entry.Title, () => new DupesWorkshop()
			{
				SortOrder = Storage.SortOrder.RankedByVote,
				Category = entry.Name.ToString()
			} );
		}


		AddGrow();
		AddHeader( "Local" );
		AddOption( "📂", "Local Dupes", () => new DupesLocal() );
	}

	protected override void OnMenuFooter( Panel footer )
	{
		footer.AddChild<DupesFooter>();
	}
}

public enum DupeCategory
{
	[Icon( "🚗" )]
	Vehicle,
	[Icon( "🤖" )]
	Robot,
	[Icon( "✈️" )]
	Plane,
	[Icon( "🕺🏼" )]
	Pose,
	[Icon( "🏹" )]
	Weapon,
	[Icon( "🖼️" )]
	Art,
	[Icon( "🏠" )]
	Scene,
	[Icon( "🎳" )]
	Game,
	[Icon( "🛸" )]
	Spaceship,
	[Icon( "🎰" )]
	Machine,
	[Icon( "🧸" )]
	Toys,
	[Icon( "🪤" )]
	Trap,
	[Icon( "⛵" )]
	Boat,
	[Icon( "📂" )]
	Other
}

public enum DupeMovement
{
	Static,
	Wheeled,
	Flying,
	Walking,
	Water,
	Tracked
}
