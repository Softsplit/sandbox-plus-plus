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
	/// How much sway/lag the viewmodel has when looking around.
	/// </summary>
	[Property, Group( "Sway" )]
	public float SwayScale { get; set; } = 1.0f;

	/// <summary>
	/// How fast the viewmodel catches up to your view rotation.
	/// </summary>
	[Property, Group( "Sway" )]
	public float SwaySpeed { get; set; } = 10.0f;

	/// <summary>
	/// Maximum sway offset in degrees.
	/// </summary>
	[Property, Group( "Sway" )]
	public float MaxSwayDegrees { get; set; } = 3.0f;

	/// <summary>
	/// Scale for the bob effect.
	/// </summary>
	[Property, Group( "Bob" )]
	public float BobScale { get; set; } = 1.0f;

	public bool IsAttacking { get; set; }

	TimeSince AttackDuration;

	// Sway tracking
	Angles lastAngles;
	Vector3 swayOffset;
	Angles swayAngles;
	bool isFirstUpdate = true;

	// Bob state
	float bobTime;
	float lastBobTime;
	float verticalBob;
	float lateralBob;

	// Bob constants
	const float BOB_CYCLE_MAX = 0.45f;
	const float BOB_UP = 0.5f;

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

	void CalcBob( PlayerController controller )
	{
		if ( !GamePreferences.ViewBobbing )
		{
			verticalBob = 0;
			lateralBob = 0;
			return;
		}

		var speed = controller.Velocity.WithZ( 0 ).Length;
		speed = Math.Clamp( speed, -320f, 320f );

		var bobOffset = speed.Remap( 0, 320, 0f, 1f );
		bobTime += (Time.Now - lastBobTime) * bobOffset;
		lastBobTime = Time.Now;

		var cycle = bobTime - MathF.Floor( bobTime / BOB_CYCLE_MAX ) * BOB_CYCLE_MAX;
		cycle /= BOB_CYCLE_MAX;

		if ( cycle < BOB_UP )
		{
			cycle = MathF.PI * cycle / BOB_UP;
		}
		else
		{
			cycle = MathF.PI + MathF.PI * (cycle - BOB_UP) / (1.0f - BOB_UP);
		}

		verticalBob = speed * 0.005f;
		verticalBob = verticalBob * 0.3f + verticalBob * 0.7f * MathF.Sin( cycle );
		verticalBob = Math.Clamp( verticalBob, -7.0f, 4.0f );

		cycle = bobTime - MathF.Floor( bobTime / (BOB_CYCLE_MAX * 2) ) * BOB_CYCLE_MAX * 2;
		cycle /= BOB_CYCLE_MAX * 2;

		if ( cycle < BOB_UP )
		{
			cycle = MathF.PI * cycle / BOB_UP;
		}
		else
		{
			cycle = MathF.PI + MathF.PI * (cycle - BOB_UP) / (1.0f - BOB_UP);
		}

		lateralBob = speed * 0.005f;
		lateralBob = lateralBob * 0.3f + lateralBob * 0.7f * MathF.Sin( cycle );
		lateralBob = Math.Clamp( lateralBob, -7.0f, 4.0f );
	}

	void CalcSway( Rotation cameraRotation )
	{
		var currentAngles = cameraRotation.Angles();

		if ( isFirstUpdate )
		{
			lastAngles = currentAngles;
			swayOffset = Vector3.Zero;
			swayAngles = Angles.Zero;
			isFirstUpdate = false;
		}

		var deltaYaw = Angles.NormalizeAngle( currentAngles.yaw - lastAngles.yaw );
		var deltaPitch = Angles.NormalizeAngle( currentAngles.pitch - lastAngles.pitch );

		var targetSwayAngles = new Angles(
			Math.Clamp( -deltaPitch * SwayScale * 2f, -MaxSwayDegrees, MaxSwayDegrees ),
			Math.Clamp( -deltaYaw * SwayScale * 2f, -MaxSwayDegrees, MaxSwayDegrees ),
			Math.Clamp( deltaYaw * SwayScale * 0.5f, -MaxSwayDegrees * 0.5f, MaxSwayDegrees * 0.5f ) // slight roll
		);

		swayAngles = Angles.Lerp( swayAngles, targetSwayAngles, Time.Delta * SwaySpeed );
		swayAngles = new Angles(
			MathX.Lerp( swayAngles.pitch, 0, Time.Delta * SwaySpeed * 0.5f ),
			MathX.Lerp( swayAngles.yaw, 0, Time.Delta * SwaySpeed * 0.5f ),
			MathX.Lerp( swayAngles.roll, 0, Time.Delta * SwaySpeed * 0.5f )
		);

		var right = cameraRotation.Right;
		var up = cameraRotation.Up;

		swayOffset = Vector3.Lerp( swayOffset, Vector3.Zero, Time.Delta * SwaySpeed );
		swayOffset += right * -deltaYaw * 0.02f * SwayScale;
		swayOffset += up * deltaPitch * 0.02f * SwayScale;
		swayOffset = swayOffset.ClampLength( 1.0f );

		lastAngles = currentAngles;
	}

	void ICameraSetup.Setup( CameraComponent cc )
	{
		Renderer.Enabled = !HideViewModel;

		WorldPosition = cc.WorldPosition;
		WorldRotation = cc.WorldRotation;

		var playerController = GetComponentInParent<PlayerController>();
		if ( playerController.IsValid() )
		{
			CalcBob( playerController );
			CalcSway( cc.WorldRotation );

			var forward = cc.WorldRotation.Forward;
			var right = cc.WorldRotation.Right;
			var up = cc.WorldRotation.Up;

			var bobPosition = Vector3.Zero;
			var bobAngles = Angles.Zero;

			bobPosition += forward * verticalBob * 0.1f * BobScale;
			bobPosition += up * verticalBob * 0.1f * BobScale;

			bobPosition += right * lateralBob * 0.8f * BobScale;

			bobAngles.roll = verticalBob * 0.5f * BobScale;
			bobAngles.pitch = -verticalBob * 0.4f * BobScale;
			bobAngles.yaw = -lateralBob * 0.3f * BobScale;

			bobPosition += swayOffset;
			bobAngles += swayAngles;

			WorldPosition += bobPosition;
			WorldRotation *= Rotation.From( bobAngles );
		}

		ApplyAnimationTransform( cc );
	}

	void ApplyAnimationTransform( CameraComponent cc )
	{
		if ( !Renderer.IsValid() ) return;

		if ( Renderer.TryGetBoneTransformLocal( "camera", out var bone ) )
		{
			var scale = 0.5f;
			cc.LocalPosition += bone.Position * scale;
			cc.LocalRotation *= bone.Rotation * scale;
		}
	}

	void UpdateAnimation()
	{
		var playerController = GetComponentInParent<PlayerController>();
		if ( !playerController.IsValid() ) return;

		var rot = Scene.Camera.WorldRotation.Angles();

		Renderer.Set( "b_twohanded", true );
		Renderer.Set( "b_grounded", playerController.IsOnGround );

		Renderer.Set( "aim_pitch", rot.pitch );
		Renderer.Set( "aim_yaw", rot.yaw );

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

	public void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		DoTracerEffect( hitPoint, origin );
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
