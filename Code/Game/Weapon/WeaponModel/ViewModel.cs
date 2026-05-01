public sealed partial class ViewModel : WeaponModel, ICameraSetup
{
	[ConVar( "sbdm.hideviewmodel", ConVarFlags.Cheat )]
	private static bool HideViewModel { get; set; } = false;

	/// <summary>
	/// Turns on incremental reloading parameters.
	/// </summary>
	[Property, Group( "Animation" )]
	public bool IsIncremental { get; set; } = false;

	/// <summary>
	/// Animation speed in general.
	/// </summary>
	[Property, Group( "Animation" )]
	public float AnimationSpeed { get; set; } = 1.0f;

	/// <summary>
	/// Animation speed for incremental reload sections.
	/// </summary>
	[Property, Group( "Animation" )]
	public float IncrementalAnimationSpeed { get; set; } = 3.0f;

	/// <summary>
	/// Use fast anims?
	/// </summary>
	[Property] 
	public bool UseFastAnimations { get; set; } = false;

	public bool IsAttacking { get; set; }

	TimeSince AttackDuration;

	bool _reloadFinishing;
	TimeSince _reloadFinishTimer;

	protected override void OnStart()
	{
		foreach ( var renderer in GetComponentsInChildren<ModelRenderer>() )
		{
			// Don't render shadows for viewmodels
			renderer.RenderType = ModelRenderer.ShadowRenderType.Off;
		}
	}

	protected override void OnUpdate()
	{
		UpdateAnimation();
	}

	void ICameraSetup.Setup( CameraComponent cc )
	{
		Renderer.Enabled = !HideViewModel;

		WorldPosition = cc.WorldPosition;
		WorldRotation = cc.WorldRotation;

		CalcViewModelView( cc );
		ApplyAnimationTransform( cc );
	}

	void ApplyAnimationTransform( CameraComponent cc )
	{
		if ( !Renderer.IsValid() ) return;

		if ( Renderer.TryGetBoneTransformLocal( "camera", out var bone ) )
		{
			var scale = 0.5f;
			cc.WorldPosition += cc.WorldRotation * bone.Position * scale;
			cc.WorldRotation *= bone.Rotation * scale;
		}
	}

	void UpdateAnimation()
	{
		var playerController = GetComponentInParent<PlayerController>();
		if ( !playerController.IsValid() ) return;

		var rot = Scene.Camera.WorldRotation.Angles();

		Renderer.Set( "b_twohanded", true );
		Renderer.Set( "deploy_type", UseFastAnimations ? 1 : 0 );
		Renderer.Set( "reload_type", UseFastAnimations ? 1 : 0 );

		Renderer.Set( "b_grounded", playerController.IsOnGround );
		Renderer.Set( "move_bob", 0.0f );

		Renderer.Set( "aim_pitch", rot.pitch );
		Renderer.Set( "aim_pitch_inertia", 0.0f );

		Renderer.Set( "aim_yaw", rot.yaw );
		Renderer.Set( "aim_yaw_inertia", 0.0f );

		Renderer.Set( "attack_hold", IsAttacking ? AttackDuration.Relative.Clamp( 0f, 1f ) : 0f );

		if ( _reloadFinishing && _reloadFinishTimer >= 0.5f )
		{
			_reloadFinishing = false;
			Renderer.Set( "speed_reload", AnimationSpeed );
			Renderer.Set( "b_reloading", false );
		}

		var velocity = playerController.Velocity;

		var dir = velocity;
		var forward = Scene.Camera.WorldRotation.Forward.Dot( dir );
		var sideward = Scene.Camera.WorldRotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Renderer.Set( "move_direction", angle );
		Renderer.Set( "move_speed", velocity.Length );
		Renderer.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
		Renderer.Set( "move_y", sideward );
		Renderer.Set( "move_x", forward );
		Renderer.Set( "move_z", velocity.z );
	}

	public override void OnAttack()
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();

		if ( IsThrowable )
		{
			Renderer?.Set( "b_throw", true );

			Invoke( 0.5f, () =>
			{
				Renderer?.Set( "b_deploy_new", true );
				Renderer?.Set( "b_pull", false );
			} );
		}
	}

	public override void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		DoTracerEffect( hitPoint, origin );
	}

	/// <summary>
	/// Called when starting to reload a weapon.
	/// </summary>
	public void OnReloadStart()
	{
		_reloadFinishing = false; // cancel any pending incremental finish from a previous reload
		Renderer?.Set( "speed_reload", AnimationSpeed );
		Renderer?.Set( IsIncremental ? "b_reloading" : "b_reload", true );
	}

	/// <summary>
	/// Called when incrementally reloading a weapon.
	/// </summary>
	public void OnIncrementalReload()
	{
		Renderer?.Set( "speed_reload", IncrementalAnimationSpeed );
		Renderer?.Set( "b_reloading_shell", true );
	}

	public void OnReloadFinish()
	{
		if ( IsIncremental )
		{
			_reloadFinishing = true;
			_reloadFinishTimer = 0;
		}
		else
		{
			Renderer?.Set( "b_reload", false );
		}
	}
}
