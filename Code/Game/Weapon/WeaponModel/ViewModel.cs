public sealed partial class ViewModel : WeaponModel, ICameraSetup
{
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

	// Entity system bobbing properties
	[Property, Group( "Bobbing" )] public bool EnableSwingAndBob { get; set; } = true;
	[Property, Group( "Bobbing" )] public float SwingInfluence { get; set; } = 0.05f;
	[Property, Group( "Bobbing" )] public float ReturnSpeed { get; set; } = 5.0f;
	[Property, Group( "Bobbing" )] public float MaxOffsetLength { get; set; } = 10.0f;
	[Property, Group( "Bobbing" )] public float BobCycleTime { get; set; } = 7;
	[Property, Group( "Bobbing" )] public Vector3 BobDirection { get; set; } = new Vector3( 0.0f, 1.0f, 0.5f );
	[Property, Group( "Bobbing" )] public float InertiaDamping { get; set; } = 20.0f;

	// Entity system bobbing state
	private Vector3 swingOffset;
	private float lastPitch;
	private float lastYaw;
	private float bobAnim;
	private float bobSpeed;
	private bool activated = false;

	public bool IsAttacking { get; set; }
	public float YawInertia { get; private set; }
	public float PitchInertia { get; private set; }

	TimeSince AttackDuration;

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

	void ICameraSetup.Setup( CameraComponent camera )
	{
		var inPos = camera.WorldPosition;
		var inRot = camera.WorldRotation;

		if ( !activated )
		{
			lastPitch = inRot.Pitch();
			lastYaw = inRot.Yaw();
			YawInertia = 0;
			PitchInertia = 0;
			activated = true;
		}

		// Apply camera bone transform first
		ApplyAnimationTransform( camera );

		// Set base position and rotation
		WorldPosition = inPos;
		WorldRotation = inRot;

		var newPitch = WorldRotation.Pitch();
		var newYaw = WorldRotation.Yaw();

		var pitchDelta = Angles.NormalizeAngle( newPitch - lastPitch );
		var yawDelta = Angles.NormalizeAngle( lastYaw - newYaw );

		PitchInertia += pitchDelta;
		YawInertia += yawDelta;

		if ( EnableSwingAndBob )
		{
			var player = GetComponentInParent<Player>();
			var playerVelocity = Vector3.Zero;

			if ( player.IsValid() && player.Controller.IsValid() )
			{
				playerVelocity = player.Controller.Velocity;

				if ( player.Controller.Tags.Has( "noclip" ) )
				{
					playerVelocity = Vector3.Zero;
				}
			}

			var verticalDelta = playerVelocity.z * Time.Delta;
			var viewDown = Rotation.FromPitch( newPitch ).Up * -1.0f;
			verticalDelta *= 1.0f - MathF.Abs( viewDown.Cross( Vector3.Down ).y );
			pitchDelta -= verticalDelta * 1.0f;

			var speed = playerVelocity.WithZ( 0 ).Length;
			speed = speed > 10.0 ? speed : 0.0f;
			bobSpeed = bobSpeed.LerpTo( speed, Time.Delta * InertiaDamping );

			var offset = CalcSwingOffset( pitchDelta, yawDelta );
			offset += CalcBobbingOffset( bobSpeed );

			WorldPosition += WorldRotation * offset;
		}
		else
		{
			Renderer.Set( "aim_pitch_inertia", PitchInertia );
			Renderer.Set( "aim_yaw_inertia", YawInertia );
		}

		lastPitch = newPitch;
		lastYaw = newYaw;

		YawInertia = YawInertia.LerpTo( 0, Time.Delta * InertiaDamping );
		PitchInertia = PitchInertia.LerpTo( 0, Time.Delta * InertiaDamping );
	}

	Vector3 CalcSwingOffset( float pitchDelta, float yawDelta )
	{
		var swingVelocity = new Vector3( 0, yawDelta, pitchDelta );

		swingOffset -= swingOffset * ReturnSpeed * Time.Delta;
		swingOffset += swingVelocity * SwingInfluence;

		if ( swingOffset.Length > MaxOffsetLength )
		{
			swingOffset = swingOffset.Normal * MaxOffsetLength;
		}

		return swingOffset;
	}

	Vector3 CalcBobbingOffset( float speed )
	{
		bobAnim += Time.Delta * BobCycleTime;

		var twoPI = MathF.PI * 2.0f;

		if ( bobAnim > twoPI )
		{
			bobAnim -= twoPI;
		}

		var offset = BobDirection * (speed * 0.005f) * MathF.Cos( bobAnim );
		offset = offset.WithZ( -MathF.Abs( offset.z ) );

		return offset;
	}

	void ApplyAnimationTransform( CameraComponent cc )
	{
		if ( !Renderer.IsValid() ) return;

		if ( Renderer.TryGetBoneTransformLocal( "camera", out var bone ) )
		{
			cc.LocalPosition += bone.Position;
			cc.LocalRotation *= bone.Rotation;
		}
	}

	void UpdateAnimation()
	{
		var playerController = GetComponentInParent<PlayerController>();
		if ( !playerController.IsValid() ) return;

		Renderer.Set( "b_grounded", playerController.IsOnGround );
		Renderer.Set( "attack_hold", IsAttacking ? AttackDuration.Relative.Clamp( 0f, 1f ) : 0f );
	}

	public void OnAttack()
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

	/// <summary>
	/// Called when starting to reload a weapon.
	/// </summary>
	public void OnReloadStart()
	{
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
			//
			// Stops the reload after a little delay so it's not immediately cancelling the animation.
			//
			Invoke( 0.5f, () =>
			{
				Renderer?.Set( "speed_reload", AnimationSpeed );
				Renderer?.Set( "b_reloading", false );
			} );
		}
		else
		{
			Renderer?.Set( "b_reload", false );
		}
	}
}
