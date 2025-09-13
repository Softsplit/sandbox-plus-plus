/// <summary>
/// Holds player information like health
/// </summary>
public sealed partial class Player : Component, Component.IDamageable, PlayerController.IEvents
{
	public static Player FindLocalPlayer() => Game.ActiveScene.GetAllComponents<Player>().Where( x => x.IsLocalPlayer ).FirstOrDefault();

	[RequireComponent] public PlayerController Controller { get; set; }
	[Property] public GameObject Body { get; set; }
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxHealth { get; set; } = 100;

	[Sync( SyncFlags.FromHost )] public PlayerData PlayerData { get; set; }

	public Transform EyeTransform
	{
		get
		{
			Assert.True( Controller.IsValid(), $"Player {DisplayName}'s PlayerController is invalid (IsValid: {this.IsValid()}, IsLocalPlayer: {IsLocalPlayer}, IsHost: {Networking.IsHost}, IsActive: {PlayerData?.Connection?.IsActive})" );
			return Controller.EyeTransform;
		}
	}
	public bool IsLocalPlayer => !IsProxy;
	public Guid PlayerId => PlayerData.PlayerId;
	public long SteamId => PlayerData.SteamId;
	public string DisplayName => PlayerData.DisplayName;

	/// <summary>
	/// True if the player wants the HUD not to draw right now
	/// </summary>
	public bool WantsHideHud
	{
		get
		{
			var weapon = GetComponent<PlayerInventory>()?.ActiveWeapon;
			if ( weapon.IsValid() && weapon.WantsHideHud )
				return true;

			return false;
		}
	}

	protected override void OnStart()
	{
		var targets = Scene.GetAllComponents<DeathCameraTarget>()
			.Where( x => x.Connection == Network.Owner );

		// We don't care about spectating corpses once we spawn
		foreach ( var t in targets )
		{
			t.GameObject.Destroy();
		}
	}

	/// <summary>
	/// Try to inherit transforms from the player onto its new ragdoll
	/// </summary>
	/// <param name="ragdoll"></param>
	private void CopyBoneScalesToRagdoll( GameObject ragdoll )
	{
		// we are only interested in the bones of the player, not anything that may be attached to it.
		var playerRenderer = Body.GetComponent<SkinnedModelRenderer>();
		var bones = playerRenderer.Model.Bones;

		var ragdollRenderer = ragdoll.GetComponent<SkinnedModelRenderer>();
		ragdollRenderer.CreateBoneObjects = true;

		var ragdollObjects = ragdoll.GetAllObjects( true ).ToDictionary( x => x.Name );

		for ( var i = 0; i <= playerRenderer.Model.BoneCount; ++i )
		{
			var boneName = playerRenderer.Model.GetBoneName( i );

			if ( !ragdollObjects.TryGetValue( boneName, out GameObject boneOnRagdoll ) )
				continue;

			var boneObject = playerRenderer.GetBoneObject( boneName );
			if ( !boneObject.IsValid() )
			{
				continue;
			}

			if ( boneOnRagdoll.IsValid() && boneObject.WorldScale != Vector3.One )
			{
				boneOnRagdoll.Flags = boneOnRagdoll.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				boneOnRagdoll.WorldScale = boneObject.WorldScale;

				var z = boneOnRagdoll.Parent;
				z.Flags = z.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				z.WorldScale = boneObject.WorldScale;
			}
		}
	}

	/// <summary>
	/// Creates a ragdoll but it isn't enabled
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void CreateRagdoll()
	{
		if ( Application.IsDedicatedServer ) return;

		var ragdoll = Controller.CreateRagdoll();
		if ( !ragdoll.IsValid() ) return;

		CopyBoneScalesToRagdoll( ragdoll );

		var corpse = ragdoll.AddComponent<DeathCameraTarget>();
		corpse.Connection = Network.Owner;
		corpse.Created = DateTime.Now;
	}

	void CreateRagdollAndGhost()
	{
		var go = new GameObject( false, "Observer" );
		go.Components.Create<PlayerObserver>();
		go.NetworkSpawn( Network.Owner );
	}

	/// <summary>
	/// Broadcasts death to other players
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	void NotifyDeath( IPlayerEvent.DiedParams args )
	{
		IPlayerEvent.PostToGameObject( GameObject, x => x.OnDied( args ) );

		if ( args.Attacker == GameObject )
		{
			IPlayerEvent.PostToGameObject( GameObject, x => x.OnSuicide() );
		}
	}

	private void Ghost()
	{
		CreateRagdollAndGhost();
	}

	/// <summary>
	/// Called on the host when a player dies
	/// </summary>
	void Kill( in DamageInfo d )
	{
		//
		// Let everyone know about the death
		//

		NotifyDeath( new IPlayerEvent.DiedParams() { Attacker = d.Attacker } );

		var inventory = GetComponent<PlayerInventory>();
		if ( inventory.IsValid() )
		{
			inventory.SwitchWeapon( null );
		}

		CreateRagdoll();

		//
		// Ghost and say goodbye to the player
		//
		Ghost();
		GameObject.Destroy();
	}

	[Rpc.Owner]
	public void EquipBestWeapon()
	{
		var inventory = GetComponent<PlayerInventory>();

		if ( inventory.IsValid() )
			inventory.SwitchWeapon( inventory.GetBestWeapon() );
	}

	protected override void OnUpdate()
	{
		if ( IsLocalPlayer )
			OnControl();
	}

	void OnControl()
	{
		if ( Input.Pressed( "noclip" ) && GetComponent<NoclipMoveMode>( true ) is { } noclip )
		{
			noclip.Enabled = !noclip.Enabled;
		}

		GetComponent<PlayerInventory>()?.OnControl();
	}

	public void OnDamage( in DamageInfo dmg )
	{
		if ( Health < 1 ) return;
		if ( PlayerData.IsGodMode ) return;

		var damage = dmg.Damage;
		if ( dmg.Tags.Contains( DamageTags.Headshot ) )
			damage *= 2;

		Health -= damage;

		// We didn't die
		if ( Health >= 1 ) return;

		GameManager.Current.OnDeath( this, dmg );

		Health = 0;
		Kill( dmg );
	}

	void PlayerController.IEvents.OnEyeAngles( ref Angles ang )
	{
		var player = Components.Get<Player>();
		var angles = ang;
		IPlayerEvent.Post( x => x.OnCameraMove( ref angles ) );
		ang = angles;
	}

	void PlayerController.IEvents.PostCameraSetup( CameraComponent camera )
	{
		// Set up initial field of view from preferences
		camera.FovAxis = CameraComponent.Axis.Vertical;
		camera.FieldOfView = Screen.CreateVerticalFieldOfView( Preferences.FieldOfView, 9.0f / 16.0f );
	}

	public T GetWeapon<T>() where T : BaseCarriable
	{
		return GetComponent<PlayerInventory>().GetWeapon<T>();
	}

	public void SwitchWeapon<T>() where T : BaseCarriable
	{
		var weapon = GetWeapon<T>();
		if ( weapon == null ) return;

		GetComponent<PlayerInventory>().SwitchWeapon( weapon );
	}
}
