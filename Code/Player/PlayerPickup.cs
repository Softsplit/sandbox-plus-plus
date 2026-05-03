public sealed class PlayerPickup : Component, IScenePhysicsEvents
{
	private const float PlayerUseRadius = 80.0f;
	private const float PlayerPickupMassLimit = 35.0f;
	private const float PlayerPickupSizeLimit = 128.0f;
	private const float PlayerPickupMassEstimateUnitVolume = 4096.0f;
	private const float PlayerPickupMassTrustRatio = 4.0f;
	private const float PlayerPickupError = 12.0f;
	private const float PlayerPickupHoldBase = 24.0f;
	private const float PlayerPickupAngularDamping = 10.0f;
	private const float PlayerPickupReducedMass = 1.0f;
	private const float PlayerPickupMaxSpeed = 1000.0f;
	private const float PlayerPickupMaxAngularSpeed = 62.83185f;
	private const float PlayerPickupDetachSpeed = 480.0f;
	private const float PlayerPickupDetachAngularSpeed = 12.56637f;
	private const float PlayerPickupAngleAlignment = 30.0f;
	private const float PlayerPickupDefaultPlayerHalfWidth = 16.0f;

	[RequireComponent] public Player Player { get; set; }

	[Sync( SyncFlags.FromHost )]
	public GameObject HeldObject { get; private set; }

	[ConVar( "player_throwforce", ConVarFlags.Cheat | ConVarFlags.Replicated | ConVarFlags.Server )]
	public static float PlayerThrowForce { get; set; } = 1000.0f;

	public bool IsHoldingObject => _isHoldingObject || HeldObject.IsValid();
	public bool IsBlockingWeaponInput => IsOwnerControllingPickup || _ownerSuppressInputUntilRelease;

	private readonly PlayerPickupController _pickupController = new();
	private bool _isHoldingObject;
	private bool _pickupEffectsActive;
	private bool _ownerIsControllingPickup;
	private bool _ownerSuppressInputUntilRelease;

	private bool IsOwnerControllingPickup => IsHoldingObject || _ownerIsControllingPickup;

	public bool OnControl()
	{
		if ( !Player.IsLocalPlayer )
			return false;

		if ( _ownerSuppressInputUntilRelease )
		{
			if ( !IsOwnerControllingPickup && !IsHeldInputDown() )
			{
				_ownerSuppressInputUntilRelease = false;
				return false;
			}

			ClearHeldInputs();
			return true;
		}

		if ( IsOwnerControllingPickup )
		{
			if ( IsDropInputPressed() )
			{
				_ownerSuppressInputUntilRelease = true;
				HostUseHeldObject( PickupUseType.Off );
			}
			else if ( IsThrowInputPressed() )
			{
				_ownerSuppressInputUntilRelease = true;
				HostUseHeldObject( PickupUseType.Throw );
			}

			ClearHeldInputs();
			return true;
		}

		if ( Input.Pressed( "use" ) && FindUseObject( out _, true ) )
		{
			_ownerIsControllingPickup = true;
			HostTryPickupObject();
			ClearHeldInputs();
			return true;
		}

		return false;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !Networking.IsHost )
			return;

		if ( !_pickupController.IsHoldingObject )
		{
			if ( IsHoldingObject )
				Shutdown();

			return;
		}

		if ( !HeldObject.IsValid() || !_pickupController.GetAttached().IsValid() )
			Shutdown();
	}

	void IScenePhysicsEvents.PrePhysicsStep()
	{
		if ( !Networking.IsHost )
			return;

		if ( !_pickupController.IsHoldingObject )
			return;

		var result = _pickupController.Use( PickupUseType.Set, Time.Delta );
		if ( result != PickupUseResult.KeepHolding )
		{
			FinishUseResult( result );
			return;
		}

		_pickupController.Simulate( Time.Delta );
	}

	void IScenePhysicsEvents.PostPhysicsStep()
	{
		if ( !Networking.IsHost )
			return;

		if ( !_pickupController.IsHoldingObject )
			return;

		_pickupController.FinishSimulate();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( Networking.IsHost )
			Shutdown();

		StopPickupEffects();
	}

	[Rpc.Host]
	private void HostTryPickupObject()
	{
		if ( !IsCallerOwner() ) return;
		if ( _pickupController.IsHoldingObject || _isHoldingObject ) return;

		if ( !FindUseObject( out var body, false ) )
		{
			OwnerCancelPickup();
			return;
		}

		if ( !PlayerPickupObject( body, Rpc.Caller ?? Player.Network.Owner ) )
		{
			OwnerCancelPickup();
			return;
		}
	}

	[Rpc.Host]
	private void HostUseHeldObject( PickupUseType useType )
	{
		if ( !IsCallerOwner() ) return;

		FinishUseResult( _pickupController.Use( useType, Time.Delta ) );
	}

	private void FinishUseResult( PickupUseResult result )
	{
		if ( result == PickupUseResult.Throw )
		{
			var body = _pickupController.GetAttached();
			Shutdown();
			Launch( body );
			return;
		}

		if ( result == PickupUseResult.Drop )
			Shutdown();
	}

	private bool IsCallerOwner()
	{
		var owner = Player.Network.Owner;
		if ( owner is null ) return false;
		if ( Rpc.Caller == owner ) return true;

		return Networking.IsHost && Rpc.Caller is null && Connection.Local == owner;
	}

	private bool FindUseObject( out Rigidbody body, bool allowProxy )
	{
		body = null;

		var eyeTransform = Player.EyeTransform;
		var traceDistance = MathF.Max( PlayerUseRadius, Player.Controller.ReachLength );
		var trace = Scene.Trace.Ray( eyeTransform.Position, eyeTransform.Position + eyeTransform.Forward * traceDistance )
			.IgnoreGameObjectHierarchy( Player.GameObject.Root )
			.WithoutTags( "player", "playercontroller", "trigger", "ragdoll" )
			.Run();

		if ( !trace.Hit || trace.Body is null ) return false;
		if ( HasUsableComponent( trace ) ) return false;
		if ( trace.Component is not Rigidbody hitBody ) return false;

		if ( !CanPickupObject( hitBody, Rpc.Caller ?? Player.Network.Owner, allowProxy ) )
			return false;

		body = hitBody;
		return true;
	}

	private bool HasUsableComponent( SceneTraceResult trace )
	{
		var hitObject = trace.Collider?.GameObject ?? trace.GameObject;
		if ( !hitObject.IsValid() ) return false;

		Component foundComponent = default;
		PlayerController.IEvents.PostToGameObject( Player.GameObject, x => foundComponent = x.GetUsableComponent( hitObject ) ?? foundComponent );
		if ( foundComponent.IsValid() ) return true;

		if ( hitObject.GetComponents<IPressable>().Any() )
			return true;

		if ( hitObject.GetComponentInParent<IPressable>( true ) is not null )
			return true;

		return false;
	}

	private bool CanPickupObject( Rigidbody body, Connection grabber, bool allowProxy = false )
	{
		if ( !body.IsValid() ) return false;
		if ( body.IsProxy && !allowProxy ) return false;
		if ( !allowProxy && !body.PhysicsBody.IsValid() ) return false;
		if ( !allowProxy && !body.MotionEnabled ) return false;
		if ( !allowProxy && PlayerPickupController.IsPlayerStandingOnBody( Player, body ) ) return false;
		if ( IsHeldByPhysgun( body ) ) return false;

		var bounds = body.GetWorldBounds();
		var size = bounds.Size;
		if ( size.x > PlayerPickupSizeLimit || size.y > PlayerPickupSizeLimit || size.z > PlayerPickupSizeLimit )
			return false;

		if ( !allowProxy && GetPickupMass( body, bounds ) > PlayerPickupMassLimit )
			return false;

		var grabEvent = new IPhysgunEvent.GrabEvent { Grabber = grabber };
		body.GameObject.Root.RunEvent<IPhysgunEvent>( x => x.OnPhysgunGrab( grabEvent ) );
		return !grabEvent.Cancelled;
	}

	private static float GetPickupMass( Rigidbody body, BBox bounds )
	{
		var explicitMass = GetExplicitPickupMass( body );
		if ( explicitMass > 0.0f ) return explicitMass;

		var estimatedMass = EstimatePickupMass( bounds.Size );
		var liveMass = GetLivePickupMass( body );
		if ( !IsUsableMass( liveMass ) ) return estimatedMass;
		if ( estimatedMass <= 0.0f ) return liveMass;
		if ( liveMass > estimatedMass * PlayerPickupMassTrustRatio ) return estimatedMass;

		return MathF.Max( liveMass, estimatedMass );
	}

	private static float GetExplicitPickupMass( Rigidbody body )
	{
		if ( !body.IsValid() ) return 0.0f;

		var mass = 0.0f;
		foreach ( var properties in body.GameObject.Root.GetComponentsInChildren<PhysicalProperties>( true ) )
		{
			if ( !properties.IsValid() || properties.Mass <= 0.0f )
				continue;

			mass += properties.Mass;
		}

		return mass;
	}

	private static float GetLivePickupMass( Rigidbody body )
	{
		if ( !body.IsValid() ) return 0.0f;
		if ( body.PhysicsBody.IsValid() ) return body.PhysicsBody.Mass;

		return body.Mass;
	}

	private static float EstimatePickupMass( Vector3 size )
	{
		var volume = MathF.Max( size.x, 0.0f ) * MathF.Max( size.y, 0.0f ) * MathF.Max( size.z, 0.0f );
		return volume / PlayerPickupMassEstimateUnitVolume;
	}

	private static bool IsUsableMass( float mass )
	{
		return mass > 0.0f && !float.IsNaN( mass ) && !float.IsInfinity( mass );
	}

	private bool IsHeldByPhysgun( Rigidbody body )
	{
		if ( !body.IsValid() ) return false;

		var root = body.GameObject.Root;
		foreach ( var physgun in Scene.GetAllComponents<Physgun>() )
		{
			if ( !physgun.IsValid() ) continue;

			var state = physgun._state;
			if ( !state.IsValid() ) continue;
			if ( state.GameObject.IsValid() && state.GameObject.Root == root ) return true;

			var heldBody = state.Body;
			if ( !heldBody.IsValid() ) continue;
			if ( heldBody.GameObject.Root == root ) return true;
		}

		return false;
	}

	private bool PlayerPickupObject( Rigidbody body, Connection grabber )
	{
		if ( !CanPickupObject( body, grabber ) )
			return false;

		if ( !_pickupController.Init( Player, body ) )
			return false;

		HeldObject = body.GameObject;
		_isHoldingObject = true;
		OwnerStartPickup();
		return true;
	}

	private void Shutdown()
	{
		var wasHolding = _pickupController.IsHoldingObject || IsHoldingObject;

		_pickupController.Shutdown( false );
		HeldObject = null;
		_isHoldingObject = false;

		if ( wasHolding )
			OwnerStopPickup();
	}

	[Rpc.Owner]
	private void OwnerStartPickup()
	{
		_ownerIsControllingPickup = true;
		_ownerSuppressInputUntilRelease = false;
		StartPickupEffects();
	}

	[Rpc.Owner]
	private void OwnerStopPickup()
	{
		StopPickupEffects();
		_ownerSuppressInputUntilRelease = IsHeldInputDown();
		ClearHeldInputs();
	}

	[Rpc.Owner]
	private void OwnerCancelPickup()
	{
		_ownerIsControllingPickup = false;
		_ownerSuppressInputUntilRelease = false;
		ClearHeldInputs();
	}

	private void StartPickupEffects()
	{
		_pickupEffectsActive = true;

		var inventory = Player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		inventory.ActiveWeapon?.HolsterForPickup();
	}

	private void StopPickupEffects()
	{
		_ownerIsControllingPickup = false;
		if ( !_pickupEffectsActive ) return;

		_pickupEffectsActive = false;

		var inventory = Player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		inventory.ActiveWeapon?.Deploy();
	}

	private void Launch( Rigidbody body )
	{
		if ( !body.IsValid() ) return;
		if ( body.IsProxy ) return;
		if ( !body.PhysicsBody.IsValid() ) return;

		var massFactor = body.PhysicsBody.Mass.Clamp( 0.5f, 15.0f ).Remap( 0.5f, 15.0f, 0.5f, 4.0f );
		var force = Player.EyeTransform.Forward * PlayerThrowForce * massFactor;

		body.ApplyImpulse( force );
		body.PhysicsBody.ApplyAngularImpulse( Vector3.Random * 10.0f * massFactor );
	}

	private static void ClearHeldInputs()
	{
		Input.Clear( "use" );
		Input.Clear( "attack1" );
		Input.Clear( "attack2" );
		Input.Clear( "drop" );
		Input.MouseWheel = default;
	}

	private static bool IsDropInputPressed()
	{
		return Input.Pressed( "use" ) || Input.Pressed( "attack2" ) || Input.Pressed( "drop" );
	}

	private static bool IsThrowInputPressed()
	{
		return Input.Pressed( "attack1" );
	}

	private static bool IsHeldInputDown()
	{
		return Input.Down( "use" ) || Input.Down( "attack1" ) || Input.Down( "attack2" ) || Input.Down( "drop" );
	}

	private enum PickupUseType
	{
		Set,
		Off,
		Throw
	}

	private enum PickupUseResult
	{
		KeepHolding,
		Drop,
		Throw
	}

	private sealed class PlayerPickupController
	{
		private readonly GrabController _grabController = new();
		private Player _player;

		public bool IsHoldingObject => _grabController.IsAttached;

		public bool Init( Player player, Rigidbody body )
		{
			Shutdown( false );

			if ( !player.IsValid() ) return false;
			if ( !body.IsValid() ) return false;
			if ( !body.PhysicsBody.IsValid() ) return false;

			_player = player;
			_grabController.SetIgnorePitch( true );
			_grabController.SetAngleAlignment( PlayerPickupAngleAlignment );
			_grabController.AttachObject( player, body );
			return true;
		}

		public void Shutdown( bool clearVelocity )
		{
			_grabController.DetachObject( clearVelocity );
			_player = null;
		}

		public PickupUseResult Use( PickupUseType useType, float delta )
		{
			var attached = _grabController.GetAttached();
			if ( !attached.IsValid() || useType == PickupUseType.Off || _grabController.ComputeError() > PlayerPickupError )
				return PickupUseResult.Drop;

			if ( !attached.PhysicsBody.IsValid() || !attached.MotionEnabled )
				return PickupUseResult.Drop;

			if ( useType == PickupUseType.Throw )
				return PickupUseResult.Throw;

			if ( useType == PickupUseType.Set && !_grabController.UpdateObject( _player, PlayerPickupError, delta ) )
				return PickupUseResult.Drop;

			return PickupUseResult.KeepHolding;
		}

		public void Simulate( float delta )
		{
			_grabController.Simulate( delta );
		}

		public void FinishSimulate()
		{
			_grabController.FinishSimulate();
		}

		public Rigidbody GetAttached()
		{
			return _grabController.GetAttached();
		}

		public static bool IsPlayerStandingOnBody( Player player, Rigidbody body )
		{
			if ( !player.IsValid() || !body.IsValid() ) return false;
			if ( !player.Controller.IsValid() ) return false;

			var groundObject = player.Controller.GroundObject;
			if ( !groundObject.IsValid() ) return false;

			var root = body.GameObject.Root;
			return groundObject == body.GameObject || groundObject == root || root.IsAncestor( groundObject );
		}
	}

	private sealed class GrabController
	{
		private Rigidbody _body;
		private Vector3 _targetPosition;
		private Rotation _targetRotation;
		private Vector3 _targetVelocity;
		private bool _hasTargetVelocity;
		private Rotation _attachedAnglesPlayerSpace;
		private Vector3 _attachedPositionObjectSpace;
		private float _distanceOffset;
		private float _angleAlignment;
		private float _savedAngularDamping;
		private float _savedMass;
		private bool _savedInterpolation;
		private bool _savedImpactDamageEnabled;
		private bool _savedUseController;
		private bool _massWasReduced;
		private bool _ignoreRelativePitch;
		private float _errorTime;
		private float _error;

		public bool IsAttached => _body.IsValid();

		public Rigidbody GetAttached()
		{
			return _body;
		}

		public void SetIgnorePitch( bool ignore )
		{
			_ignoreRelativePitch = ignore;
		}

		public void SetAngleAlignment( float angleAlignment )
		{
			_angleAlignment = angleAlignment;
		}

		public void AttachObject( Player player, Rigidbody body )
		{
			DetachObject( false );

			_body = body;
			_savedAngularDamping = body.AngularDamping;
			_savedInterpolation = body.GameObject.Network.Interpolation;
			_savedImpactDamageEnabled = body.EnableImpactDamage;

			body.AngularDamping = PlayerPickupAngularDamping;
			body.GameObject.Network.Interpolation = false;
			body.EnableImpactDamage = false;
			body.MotionEnabled = true;

			if ( body.PhysicsBody.IsValid() )
			{
				_savedUseController = body.PhysicsBody.UseController;
				_savedMass = body.PhysicsBody.Mass;

				if ( _savedMass > PlayerPickupReducedMass )
				{
					body.PhysicsBody.Mass = PlayerPickupReducedMass;
					_massWasReduced = true;
				}
			}

			var bodyTransform = body.WorldTransform.WithScale( 1.0f );
			var center = body.GetWorldBounds().Center;
			_attachedPositionObjectSpace = bodyTransform.PointToLocal( center );
			_attachedAnglesPlayerSpace = TransformAnglesToPlayerSpace( bodyTransform.Rotation, player );

			if ( _angleAlignment != 0.0f )
				_attachedAnglesPlayerSpace = _attachedAnglesPlayerSpace.Angles().SnapToGrid( _angleAlignment );

			_distanceOffset = 0.0f;
			_errorTime = -1.0f;
			_error = 0.0f;

			SetTargetPosition( center - bodyTransform.Rotation * _attachedPositionObjectSpace, bodyTransform.Rotation );
			UpdateObject( player, PlayerPickupError, Time.Delta );
		}

		public void DetachObject( bool clearVelocity = false )
		{
			if ( _body.IsValid() )
			{
				_body.AngularDamping = _savedAngularDamping;
				_body.GameObject.Network.Interpolation = _savedInterpolation;
				_body.EnableImpactDamage = _savedImpactDamageEnabled;

				if ( _body.PhysicsBody.IsValid() )
				{
					_body.PhysicsBody.UseController = _savedUseController;

					if ( _massWasReduced )
						_body.PhysicsBody.Mass = _savedMass;

					if ( clearVelocity )
					{
						_body.PhysicsBody.Velocity = Vector3.Zero;
						_body.PhysicsBody.AngularVelocity = Vector3.Zero;
					}
					else
					{
						_body.PhysicsBody.Velocity = _body.PhysicsBody.Velocity.ClampLength( PlayerPickupDetachSpeed );
						_body.PhysicsBody.AngularVelocity = _body.PhysicsBody.AngularVelocity.ClampLength( PlayerPickupDetachAngularSpeed );
					}
				}
			}

			_body = null;
			_targetPosition = default;
			_targetRotation = Rotation.Identity;
			_attachedAnglesPlayerSpace = Rotation.Identity;
			_attachedPositionObjectSpace = default;
			_targetVelocity = default;
			_hasTargetVelocity = false;
			_distanceOffset = 0.0f;
			_savedAngularDamping = 0.0f;
			_savedMass = 0.0f;
			_savedInterpolation = false;
			_savedImpactDamageEnabled = false;
			_savedUseController = false;
			_massWasReduced = false;
			_errorTime = 0.0f;
			_error = 0.0f;
		}

		public bool UpdateObject( Player player, float errorLimit, float delta )
		{
			if ( ComputeError() > errorLimit ) return false;
			if ( !_body.IsValid() ) return false;
			if ( _body.IsProxy ) return false;
			if ( !_body.MotionEnabled ) return false;
			if ( !_body.PhysicsBody.IsValid() ) return false;
			if ( PlayerPickupController.IsPlayerStandingOnBody( player, _body ) ) return false;

			var angles = player.Controller.EyeAngles;
			angles.pitch = angles.pitch.Clamp( -75.0f, 75.0f );

			var shootPosition = GetShootPosition( player, delta, out var playerPosition );
			var eyeRotation = angles.ToRotation();
			var forward = eyeRotation.Forward;
			var bounds = _body.GetWorldBounds();
			var center = GetWorldSpaceCenter();
			var radius = GetPlayerRadius( player ) + GetCollideExtent( _body.PhysicsBody, center, bounds, forward );
			var distance = PlayerPickupHoldBase + radius * 2.0f + _distanceOffset;
			var end = TraceCollideAgainstStatic( player, _body, shootPosition, forward, distance, radius );

			end = PushTargetOutsidePlayer( player, end, forward, radius, playerPosition );

			var targetRotation = TransformAnglesFromPlayerSpace( _attachedAnglesPlayerSpace, player );
			var targetPosition = end - targetRotation * _attachedPositionObjectSpace;
			_targetVelocity = _hasTargetVelocity && delta > 0.0f ? (targetPosition - _targetPosition) / delta : Vector3.Zero;
			_hasTargetVelocity = true;
			SetTargetPosition( targetPosition, targetRotation );

			return true;
		}

		public float ComputeError()
		{
			if ( _errorTime <= 0.0f ) return 0.0f;
			if ( !_body.IsValid() ) return 9999.0f;
			if ( !_body.PhysicsBody.IsValid() ) return 9999.0f;

			_errorTime = MathF.Min( _errorTime, 1.0f );

			var error = Vector3.DistanceBetween( _targetPosition, _body.PhysicsBody.Position );
			var speed = error / _errorTime;
			if ( speed > PlayerPickupMaxSpeed )
				error *= 0.5f;

			_error = (1.0f - _errorTime) * _error + error * _errorTime;
			_errorTime = 0.0f;

			return _error;
		}

		public void Simulate( float delta )
		{
			if ( !_body.IsValid() ) return;
			if ( _body.IsProxy ) return;

			var physicsBody = _body.PhysicsBody;
			if ( !physicsBody.IsValid() ) return;

			physicsBody.Sleeping = false;
			physicsBody.UseController = true;
			physicsBody.Move( new Transform( _targetPosition, _targetRotation ), MathF.Max( delta, 0.001f ) );
			physicsBody.Velocity = physicsBody.Velocity.ClampLength( PlayerPickupMaxSpeed );
			physicsBody.AngularVelocity = physicsBody.AngularVelocity.ClampLength( PlayerPickupMaxAngularSpeed );
			_errorTime += MathF.Max( delta, 0.0f );
		}

		public void FinishSimulate()
		{
			if ( !_body.IsValid() ) return;
			if ( _body.IsProxy ) return;

			var physicsBody = _body.PhysicsBody;
			if ( !physicsBody.IsValid() ) return;

			physicsBody.Velocity = _targetVelocity.ClampLength( PlayerPickupMaxSpeed );
			physicsBody.AngularVelocity = physicsBody.AngularVelocity.ClampLength( PlayerPickupMaxAngularSpeed );
		}

		private void SetTargetPosition( Vector3 targetPosition, Rotation targetRotation )
		{
			_targetPosition = targetPosition;
			_targetRotation = targetRotation;
		}

		private Vector3 GetWorldSpaceCenter()
		{
			if ( !_body.IsValid() ) return default;

			var transform = _body.PhysicsBody.IsValid()
				? _body.PhysicsBody.Transform.WithScale( 1.0f )
				: _body.WorldTransform.WithScale( 1.0f );

			return transform.PointToWorld( _attachedPositionObjectSpace );
		}

		private Rotation TransformAnglesToPlayerSpace( Rotation angles, Player player )
		{
			if ( _ignoreRelativePitch )
				return Rotation.FromYaw( player.Controller.EyeAngles.yaw ).Inverse * angles;

			return player.WorldRotation.Inverse * angles;
		}

		private Rotation TransformAnglesFromPlayerSpace( Rotation angles, Player player )
		{
			if ( _ignoreRelativePitch )
				return Rotation.FromYaw( player.Controller.EyeAngles.yaw ) * angles;

			return player.WorldRotation * angles;
		}

		private static Vector3 GetShootPosition( Player player, float delta, out Vector3 playerPosition )
		{
			if ( !player.Controller.IsValid() )
			{
				playerPosition = player.WorldPosition;
				return player.EyeTransform.Position;
			}

			var controller = player.Controller;
			playerPosition = controller.WorldPosition;
			var playerBody = controller.Body;

			if ( playerBody.IsValid() && playerBody.PhysicsBody.IsValid() )
			{
				var physicsBody = playerBody.PhysicsBody;
				playerPosition = physicsBody.Position + physicsBody.Velocity * MathF.Max( delta, 0.0f );
			}

			return playerPosition + Vector3.Up * (controller.CurrentHeight - controller.EyeDistanceFromTop);
		}

		private static float GetPlayerRadius( Player player )
		{
			var playerHalfWidth = player.Controller.IsValid()
				? player.Controller.BodyRadius * 0.5f
				: PlayerPickupDefaultPlayerHalfWidth;

			return MathF.Sqrt( playerHalfWidth * playerHalfWidth * 2.0f );
		}

		private static float GetCollideExtent( PhysicsBody physicsBody, Vector3 center, BBox bounds, Vector3 forward )
		{
			var aabbExtent = GetProjectedExtent( bounds.Extents, forward );
			if ( !physicsBody.IsValid() ) return aabbExtent;

			var probeDistance = MathF.Max( bounds.Size.Length + PlayerPickupHoldBase, PlayerPickupSizeLimit );
			var supportPoint = physicsBody.FindClosestPoint( center - forward * probeDistance );
			if ( supportPoint.IsNaN || supportPoint.IsInfinity ) return aabbExtent;

			var extent = MathF.Abs( Vector3.Dot( forward, supportPoint - center ) );
			return extent.Clamp( 0.0f, aabbExtent );
		}

		private static float GetProjectedExtent( Vector3 extents, Vector3 forward )
		{
			return MathF.Abs( extents.x * forward.x )
				+ MathF.Abs( extents.y * forward.y )
				+ MathF.Abs( extents.z * forward.z );
		}

		private static Vector3 TraceCollideAgainstStatic( Player player, Rigidbody body, Vector3 start, Vector3 forward, float distance, float radius )
		{
			var end = start + forward * distance;
			var trace = player.Scene.Trace.Ray( start, end )
				.IgnoreGameObjectHierarchy( player.GameObject.Root )
				.IgnoreGameObjectHierarchy( body.GameObject.Root )
				.WithoutTags( "player", "playercontroller", "trigger", "ragdoll" )
				.IgnoreDynamic()
				.Run();

			if ( !trace.Hit )
				return end;

			if ( trace.Fraction < 0.5f )
				return start + forward * (radius * 0.5f);

			return start + forward * (distance - radius);
		}

		private static Vector3 PushTargetOutsidePlayer( Player player, Vector3 end, Vector3 forward, float radius, Vector3 playerPosition )
		{
			if ( !player.Controller.IsValid() ) return end;

			var playerLine = playerPosition;

			var nearest = ClosestPointOnLine( end, playerLine, playerLine + Vector3.Up * player.Controller.CurrentHeight );
			var offset = end - nearest;
			var length = offset.Length;
			if ( length >= radius ) return end;

			var direction = length > 0.001f ? offset / length : forward;
			return nearest + direction * radius;
		}

		private static Vector3 ClosestPointOnLine( Vector3 point, Vector3 start, Vector3 end )
		{
			var line = end - start;
			var lengthSquared = line.LengthSquared;
			if ( lengthSquared <= 0.0f ) return start;

			var fraction = Vector3.Dot( point - start, line ) / lengthSquared;
			fraction = fraction.Clamp( 0.0f, 1.0f );
			return start + line * fraction;
		}
	}
}