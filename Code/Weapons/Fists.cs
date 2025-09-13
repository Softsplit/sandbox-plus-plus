public class Fists : BaseCarriable
{
	[Property] public float PrimaryRate { get; set; } = 0.5f;
	[Property] public float SecondaryRate { get; set; } = 0.5f;
	[Property] public float MeleeDamage { get; set; } = 25.0f;
	[Property] public float MeleeRange { get; set; } = 80.0f;
	[Property] public float MeleeRadius { get; set; } = 20.0f;

	private TimeSince timeSinceAttack = 0;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Pressed( "attack1" ) && CanAttack( PrimaryRate ) )
		{
			AttackPrimary();
		}

		if ( Input.Pressed( "attack2" ) && CanAttack( SecondaryRate ) )
		{
			AttackSecondary();
		}
	}

	private bool CanAttack( float rate )
	{
		return timeSinceAttack >= rate;
	}

	public void AttackPrimary()
	{
		Attack( true );
	}

	public void AttackSecondary()
	{
		Attack( false );
	}

	private void Attack( bool leftHand )
	{
		if ( MeleeAttack() )
		{
			OnMeleeHit( leftHand );
		}
		else
		{
			OnMeleeMiss( leftHand );
		}

		Owner.Controller.Renderer.Set( "b_attack", true );
		timeSinceAttack = 0;
	}

	private bool MeleeAttack()
	{
		var player = Owner;
		if ( !player.IsValid() )
			return false;

		var ray = player.EyeTransform;
		var forward = ray.Rotation.Forward.Normal;

		bool hit = false;

		foreach ( var tr in TraceMelee( ray.Position, ray.Position + forward * MeleeRange, MeleeRadius ) )
		{
			if ( !tr.GameObject.IsValid() )
				continue;

			hit = true;

			if ( !Networking.IsHost )
				continue;

			// Apply damage on server
			var attackInfo = TraceAttackInfo.From( tr, MeleeDamage, localise: false );
			ShootEffects( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface );
			TraceAttack( attackInfo );
		}

		return hit;
	}

	private IEnumerable<SceneTraceResult> TraceMelee( Vector3 start, Vector3 end, float radius = 2.0f )
	{
		var trace = Scene.Trace.Ray( start, end )
			.UseHitboxes()
			.WithAnyTags( "solid", "player", "npc", "glass" )
			.IgnoreGameObjectHierarchy( Owner.GameObject );

		var tr = trace.Run();

		if ( tr.Hit )
		{
			yield return tr;
		}
		else
		{
			trace = trace.Radius( radius );

			tr = trace.Run();

			if ( tr.Hit )
			{
				yield return tr;
			}
		}
	}

	[Rpc.Broadcast]
	private void OnMeleeMiss( bool leftHand )
	{
		var viewModel = ViewModel?.GetComponent<ViewModel>();
		if ( viewModel.IsValid() )
		{
			viewModel.Renderer?.Set( "b_attack_has_hit", false );
			viewModel.Renderer?.Set( "b_attack", true );
			viewModel.Renderer?.Set( "holdtype_attack", leftHand ? 2 : 1 );
		}
	}

	[Rpc.Broadcast]
	private void OnMeleeHit( bool leftHand )
	{
		var viewModel = ViewModel?.GetComponent<ViewModel>();
		if ( viewModel.IsValid() )
		{
			viewModel.Renderer?.Set( "b_attack_has_hit", true );
			viewModel.Renderer?.Set( "b_attack", true );
			viewModel.Renderer?.Set( "holdtype_attack", leftHand ? 2 : 1 );
		}
	}

	public override void OnAdded( Player player )
	{
		base.OnAdded( player );

		var viewModel = ViewModel.GetComponent<ViewModel>();
		if ( viewModel.IsValid() )
		{
			viewModel.Renderer?.Set( "b_twohanded", false );
		}
	}

	[Rpc.Broadcast]
	public void ShootEffects( Vector3 hitpoint, bool hit, Vector3 normal, GameObject hitObject, Surface hitSurface )
	{
		if ( Application.IsDedicatedServer ) return;

		if ( hit )
		{
			var prefab = hitSurface.PrefabCollection.BulletImpact;
			prefab ??= hitSurface.GetBaseSurface()?.PrefabCollection.BulletImpact;

			if ( prefab is not null )
			{
				var fwd = Rotation.LookAt( normal * -1.0f, Vector3.Random );

				var impact = prefab.Clone();
				impact.WorldPosition = hitpoint;
				impact.WorldRotation = fwd;
				impact.SetParent( hitObject, true );

				Sound.Play( hitSurface.SoundCollection.Bullet, impact.WorldPosition );
			}
		}
	}
}
