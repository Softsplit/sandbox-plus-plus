public class Flashlight : BaseCarriable
{
	[Property] public SpotLight SpotLight { get; set; }
	[Property] public float SecondaryRate { get; set; } = 0.5f;
	[Property] public float MeleeDamage { get; set; } = 25.0f;
	[Property] public float MeleeRange { get; set; } = 80.0f;
	[Property] public float MeleeRadius { get; set; } = 20.0f;

	[Sync( SyncFlags.FromHost )] public bool LightEnabled { get; set; } = true;

	private TimeSince timeSinceLightToggled = 0;
	private TimeSince timeSinceAttack = 0;

	public override void OnEquipped( Player player )
	{
		base.OnEquipped( player );

		if ( SpotLight.IsValid() )
		{
			SpotLight.Enabled = LightEnabled;
		}
	}

	public override void OnHolstered( Player player )
	{
		base.OnHolstered( player );

		if ( SpotLight.IsValid() )
		{
			SpotLight.Enabled = false;
		}
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		// Update spotlight position and rotation to follow the muzzle transform
		if ( SpotLight.IsValid() )
		{
			var muzzleTransform = WeaponModel?.MuzzleTransform?.WorldTransform ?? WorldTransform;

			// Apply the offset to position the light in front of the weapon
			var lightOffset = Vector3.Forward * 10f;
			var worldLightPosition = muzzleTransform.PointToWorld( lightOffset );

			SpotLight.WorldPosition = worldLightPosition;
			SpotLight.WorldRotation = muzzleTransform.Rotation;

			// Handle first-person vs third-person rendering tags
			var isFirstPerson = !Owner?.Controller?.ThirdPerson ?? false;
			SpotLight.Tags.Set( "firstperson", isFirstPerson );

			// Ensure the light state matches the synchronized property
			SpotLight.Enabled = LightEnabled && Owner.IsValid();
		}
	}

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( player is null ) return;

		bool toggle = Input.Pressed( "flashlight" ) || Input.Pressed( "attack1" );

		if ( timeSinceLightToggled > 0.1f && toggle )
		{
			ToggleLightRequest();
		}

		if ( Input.Pressed( "attack2" ) && CanAttack() )
		{
			AttackSecondary();
		}
	}

	[Rpc.Host]
	private void ToggleLightRequest()
	{
		LightEnabled = !LightEnabled;
		Sound.Play( LightEnabled ? "flashlight-on" : "flashlight-off", WorldPosition );
		timeSinceLightToggled = 0;
	}

	private bool CanAttack()
	{
		return timeSinceAttack >= SecondaryRate;
	}

	public void AttackSecondary()
	{
		if ( MeleeAttack() )
		{
			OnMeleeHit();
		}
		else
		{
			OnMeleeMiss();
		}

		Sound.Play( "rust_flashlight.attack", WorldPosition );
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
	private void OnMeleeMiss()
	{
		var viewModel = ViewModel?.GetComponent<ViewModel>();
		if ( viewModel.IsValid() )
		{
			viewModel.Renderer?.Set( "attack", true );
		}
	}

	[Rpc.Broadcast]
	private void OnMeleeHit()
	{
		var viewModel = ViewModel?.GetComponent<ViewModel>();
		if ( viewModel.IsValid() )
		{
			viewModel.Renderer?.Set( "attack_hit", true );
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
