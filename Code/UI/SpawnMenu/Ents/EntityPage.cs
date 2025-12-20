
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Entity" ), Order( 2000 ), Icon( "🧠" )]
public class EntityPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddOption( "🧠", "All", () => new EntityListCloud() { Query = "sort:newest" } );
		AddOption( "⭐", "Favourites", () => new EntityListCloud() { Query = "sort:favourite" } );

		AddHeader( "Categories" );
		AddOption( "🐵", "Animals", () => new EntityListCloud() { Query = "cat:animal" } );
		AddOption( "🥁", "Audio", () => new EntityListCloud() { Query = "cat:audio" } );
		AddOption( "✨", "Effect", () => new EntityListCloud() { Query = "cat:effect" } );
		AddOption( "🥼", "Npc", () => new EntityListCloud() { Query = "cat:npc" } );
		AddOption( "🎈", "Other", () => new EntityListCloud() { Query = "cat:other" } );
		AddOption( "💪", "Showcase", () => new EntityListCloud() { Query = "cat:showcase" } );
		AddOption( "🧸", "Toys & Fun", () => new EntityListCloud() { Query = "cat:toy" } );
		AddOption( "🚚", "Vehicle", () => new EntityListCloud() { Query = "cat:vehicle" } );

		if ( Application.IsEditor )
		{
			AddGrow();
			AddOption( "📂", "Local Entities", () => new EntityListLocal() { } );
		}
	}
}
