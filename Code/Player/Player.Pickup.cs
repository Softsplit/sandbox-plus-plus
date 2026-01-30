using Sandbox.Physics;

public sealed partial class Player : PlayerController.IEvents
{
	private const float AngleAlignmentCosine = 0.866025403784f;
	private const float MaxError = 12f;
	private const float MaxPickupMass = 35f;
	private const float MaxPickupSize = 128f;
	private const float ThrowForce = 1000f;

	[Sync] public GameObject CarriedObject { get; private set; }
	public TimeSince TimeSincePickupDropped { get; private set; } = 100f;

	private Rigidbody CarriedBody { get; set; }
	private BaseCarryable WeaponBeforePickup { get; set; }
	private float SavedMass { get; set; }
	private float SavedRotDamping { get; set; }
	private Rotation AttachedAnglesPlayerSpace { get; set; }
	private Vector3 AttachedPositionObjectSpace { get; set; }
	private Sandbox.Physics.ControlJoint CarryJoint { get; set; }
	private PhysicsBody CarryTargetBody { get; set; }
	private float ErrorTime { get; set; }
	private float Error { get; set; }

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		FixedUpdateCarriedObject();
	}

	public bool CanPickupObject( GameObject obj )
	{
		obj = obj.Root;

		if ( !obj.IsValid() || obj == CarriedObject )
			return false;

		var body = obj.GetComponent<Rigidbody>();
		if ( !body.IsValid() )
			return false;

		if ( !body.MotionEnabled )
			return false;

		var renderer = obj.GetComponent<ModelRenderer>();
		var modelBounds = renderer.IsValid() && renderer.Model.IsValid() ? renderer.Model.Bounds : obj.GetBounds();
		var volume = modelBounds.Size.x * modelBounds.Size.y * modelBounds.Size.z;
		var actualMass = body.Mass;
		var estimatedMass = volume * 0.0001f;
		var effectiveMass = actualMass < 1f ? estimatedMass : MathF.Min( actualMass, estimatedMass * 2f );

		if ( effectiveMass > MaxPickupMass )
			return false;

		var size = modelBounds.Size;
		if ( size.x > MaxPickupSize || size.y > MaxPickupSize || size.z > MaxPickupSize )
			return false;

		if ( Controller.GroundObject == obj )
			return false;

		if ( obj.GetComponent<BaseCarryable>() != null )
			return false;

		if ( obj.GetComponent<Player>() != null )
			return false;

		if ( obj.GetComponent<ModelPhysics>() != null )
			return false;

		if ( obj.GetComponent<BaseChair>() != null )
			return false;

		return true;
	}

	Component PlayerController.IEvents.GetUsableComponent( GameObject go )
	{
		if ( CanPickupObject( go ) )
			return go.GetComponent<Rigidbody>();

		var current = go.Parent;
		while ( current.IsValid() )
		{
			if ( CanPickupObject( current ) )
			{
				return current.GetComponent<Rigidbody>();
			}
			current = current.Parent;
		}

		return null;
	}

	void PlayerController.IEvents.StartPressing( Component target )
	{
		if ( CarriedObject.IsValid() )
		{
			Drop( false );
			return;
		}

		if ( target is Rigidbody body && CanPickupObject( body.GameObject ) )
		{
			Pickup( body.GameObject.Root );
		}
	}

	void PlayerController.IEvents.StopPressing( Component target ) { }

	void PlayerController.IEvents.FailPressing()
	{
		if ( CarriedObject.IsValid() )
			Drop( false );
	}

	private void Pickup( GameObject obj )
	{
		if ( !obj.IsValid() ) return;
		if ( CarriedObject.IsValid() ) return;
		if ( !CanPickupObject( obj ) ) return;

		var body = obj.GetComponent<Rigidbody>();
		if ( !body.IsValid() ) return;

		CarriedObject = obj;
		CarriedObject.Network.TakeOwnership();

		var inventory = GetComponent<PlayerInventory>();
		if ( inventory.IsValid() && inventory.ActiveWeapon.IsValid() )
		{
			WeaponBeforePickup = inventory.ActiveWeapon;
			inventory.SwitchWeapon( null );
		}
	}

	private void Drop( bool thrown )
	{
		if ( !CarriedObject.IsValid() )
		{
			CarriedObject = null;
			CarriedBody = null;
			return;
		}

		var body = CarriedBody;

		RemoveCarryJoint();

		if ( body.IsValid() && body.PhysicsBody != null )
		{
			body.MassOverride = SavedMass;
			body.AngularDamping = SavedRotDamping;

			if ( thrown )
			{
				var clampedMass = MathX.Clamp( SavedMass, 0.5f, 15f );
				var massFactor = MathX.Remap( clampedMass, 0.5f, 15f, 0.5f, 4f );
				var throwVelocity = ThrowForce * massFactor / SavedMass;
				body.Velocity = Controller.Velocity + Controller.EyeAngles.ToRotation().Forward * throwVelocity;
			}
			else
			{
				if ( body.Velocity.Length > 400f )
					body.Velocity = body.Velocity.Normal * 400f;
				if ( body.AngularVelocity.Length > 720f )
					body.AngularVelocity = body.AngularVelocity.Normal * 720f;
			}
		}

		CarriedObject = null;
		CarriedBody = null;

		var inventory = GetComponent<PlayerInventory>();
		if ( inventory.IsValid() )
		{
			inventory.SwitchWeapon( WeaponBeforePickup.IsValid() ? WeaponBeforePickup : inventory.GetBestWeapon() );
			WeaponBeforePickup = null;
		}

		Input.Clear( "attack1" );
		Input.Clear( "attack2" );

		TimeSincePickupDropped = 0f;
	}

	private void UpdateCarriedObject()
	{
		if ( !CarriedObject.IsValid() )
			return;

		if ( !CarriedBody.IsValid() )
			CarriedBody = CarriedObject.GetComponent<Rigidbody>();

		if ( !CarriedBody.IsValid() )
		{
			Drop( false );
			return;
		}

		if ( !CarriedBody.MotionEnabled || Controller.GroundObject == CarriedObject )
		{
			Drop( false );
			return;
		}

		if ( Input.Down( "attack2" ) )
		{
			Input.Clear( "attack2" );
			Drop( false );
			return;
		}

		if ( Input.Pressed( "attack1" ) )
		{
			Input.Clear( "attack1" );
			Drop( true );
			return;
		}
	}

	private void FixedUpdateCarriedObject()
	{
		if ( !CarriedObject.IsValid() )
		{
			RemoveCarryJoint();
			CarriedBody = null;
			return;
		}

		if ( !CarriedBody.IsValid() )
		{
			CarriedBody = CarriedObject.GetComponent<Rigidbody>();
		}

		if ( !CarriedBody.IsValid() )
		{
			RemoveCarryJoint();
			return;
		}

		var pBody = CarriedBody.PhysicsBody;
		if ( pBody == null )
		{
			RemoveCarryJoint();
			return;
		}

		if ( CarryJoint == null )
		{
			SavedMass = CarriedBody.Mass;
			SavedRotDamping = CarriedBody.AngularDamping;
			CarriedBody.MassOverride = 1f;
			CarriedBody.AngularDamping = 10f;

			var initRenderer = CarriedObject.GetComponent<ModelRenderer>();
			var initModelBounds = initRenderer.IsValid() && initRenderer.Model.IsValid() ? initRenderer.Model.Bounds : new BBox( Vector3.One * -8f, Vector3.One * 8f );

			var playerYawRot = Rotation.FromYaw( Controller.EyeAngles.yaw );
			AttachedAnglesPlayerSpace = AlignAngles( playerYawRot.Inverse * CarriedBody.WorldRotation, AngleAlignmentCosine );
			AttachedPositionObjectSpace = CarriedBody.WorldTransform.PointToLocal( CarriedBody.WorldTransform.PointToWorld( initModelBounds.Center ) );

			CarriedBody.Velocity = Vector3.Zero;
			CarriedBody.AngularVelocity = Vector3.Zero;
			ErrorTime = -1f;
			Error = 0f;

			CarryTargetBody = new PhysicsBody( Scene.PhysicsWorld )
			{
				BodyType = PhysicsBodyType.Keyframed,
				AutoSleep = false
			};

			var maxForce = pBody.Mass * pBody.World.Gravity.LengthSquared;

			CarryJoint = PhysicsJoint.CreateControl( new PhysicsPoint( CarryTargetBody ), new PhysicsPoint( pBody, Vector3.Zero ) );
			CarryJoint.LinearSpring = new PhysicsSpring( 30f, 1f, maxForce );
			CarryJoint.AngularSpring = new PhysicsSpring( 30f, 1f, maxForce * 3f );
		}

		if ( ComputeError() > MaxError && Network.IsOwner )
		{
			Drop( false );
			return;
		}

		var pitch = MathX.Clamp( Controller.EyeAngles.pitch, -75f, 75f );
		var playerAngles = new Angles( pitch, Controller.EyeAngles.yaw, 0f );
		var eyeRot = playerAngles.ToRotation();
		var forward = eyeRot.Forward;

		var renderer = CarriedObject.GetComponent<ModelRenderer>();
		var modelBounds = renderer.IsValid() && renderer.Model.IsValid() ? renderer.Model.Bounds : new BBox( Vector3.One * -8f, Vector3.One * 8f );
		var halfSize = modelBounds.Size * 0.5f;
		var objectRot = CarriedBody.WorldRotation;
		var radialExtent = MathF.Abs( Vector3.Dot( forward, objectRot.Forward * halfSize.x ) ) +
						   MathF.Abs( Vector3.Dot( forward, objectRot.Right * halfSize.y ) ) +
						   MathF.Abs( Vector3.Dot( forward, objectRot.Up * halfSize.z ) );

		var radius = 16f + radialExtent;
		var distance = 24f + radius * 2f;
		var start = Controller.EyePosition;
		var end = start + forward * distance;

		var tr = Scene.Trace
			.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.IgnoreGameObjectHierarchy( CarriedObject )
			.WithoutTags( "player" )
			.Run();

		if ( tr.Fraction < 0.5f )
			end = start + forward * radius * 0.5f;
		else if ( tr.Fraction <= 1f )
			end = start + forward * (distance - radius);

		var playerLineBottom = WorldPosition;
		var playerLineTop = WorldPosition + Vector3.Up * 72f;
		var lineDir = (playerLineTop - playerLineBottom).Normal;
		var projLen = MathX.Clamp( Vector3.Dot( end - playerLineBottom, lineDir ), 0f, 72f );
		var nearest = playerLineBottom + lineDir * projLen;
		var delta = end - nearest;
		if ( delta.Length > 0.001f && delta.Length < radius )
			end = nearest + delta.Normal * radius;

		var targetRot = Rotation.FromYaw( playerAngles.yaw ) * AttachedAnglesPlayerSpace;
		var targetPos = end - targetRot * AttachedPositionObjectSpace;

		CarryTargetBody.Transform = new Transform( targetPos, targetRot );
		ErrorTime += Time.Delta;
	}

	private void RemoveCarryJoint()
	{
		CarryJoint?.Remove();
		CarryJoint = null;
		CarryTargetBody?.Remove();
		CarryTargetBody = null;
	}

	private float ComputeError()
	{
		if ( ErrorTime <= 0f )
			return 0f;

		if ( !CarriedBody.IsValid() || CarryTargetBody == null )
			return 9999f;

		var error = Vector3.DistanceBetween( CarryTargetBody.Position, CarriedBody.WorldPosition );
		var errorTime = MathF.Min( ErrorTime, 1f );

		if ( error / errorTime > 1000f )
			error *= 0.5f;

		Error = (1f - errorTime) * Error + error * errorTime;
		ErrorTime = 0f;
		return Error;
	}

	private static Rotation AlignAngles( Rotation rotation, float cosineAlignAngle )
	{
		var forward = TryAlignVector( rotation.Forward, cosineAlignAngle );
		var up = TryAlignVector( rotation.Up, cosineAlignAngle );
		return Rotation.LookAt( forward, up );
	}

	private static Vector3 TryAlignVector( Vector3 vec, float cosineAlignAngle )
	{
		for ( int i = 0; i < 3; i++ )
		{
			float c = i == 0 ? vec.x : (i == 1 ? vec.y : vec.z);
			if ( MathF.Abs( c ) > cosineAlignAngle )
			{
				float sign = c < 0 ? -1f : 1f;
				return i == 0 ? new Vector3( sign, 0, 0 ) :
					   i == 1 ? new Vector3( 0, sign, 0 ) :
								new Vector3( 0, 0, sign );
			}
		}
		return vec;
	}
}
