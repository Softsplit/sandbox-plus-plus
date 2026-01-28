[Icon( "🧨" )]
[ClassName( "remover" )]
[Group( "Tools" )]
public class Remover : ToolMode
{
	bool CanDestroy( GameObject go )
	{
		if ( !go.IsValid() ) return false;
		if ( !go.Tags.Contains( "removable" ) ) return false;

		return true;
	}

	public override void OnControl()
	{
		base.OnControl();

		if ( Input.Pressed( "attack1" ) )
		{
			var select = TraceSelect();
			if ( !select.IsValid() ) return;

			var target = select.GameObject?.Network?.RootGameObject;
			if ( !target.IsValid() ) return;
			if ( !CanDestroy( target ) ) return;

			Remove( target );
			ShootEffects( select );
		}
	}

	[Rpc.Host]
	public void Remove( GameObject go )
	{
		go = go?.Network?.RootGameObject;

		if ( !CanDestroy( go ) ) return;
		if ( go.IsProxy ) return;

		go.Destroy();

		var connection = Rpc.Caller;
		if ( connection is not null )
		{
			using ( Rpc.FilterInclude( connection ) )
			{
				IncrementDestroyedStat();
			}
		}
	}

	[Rpc.Broadcast]
	private static void IncrementDestroyedStat()
	{
		Sandbox.Services.Stats.Increment( "things_destroyed", 1 );
	}
}
