using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;


/// <summary>
/// Controller for AnimationClip supported blending animation
/// </summary>
public class SimplestAnimationState {
	public enum STATE {
		NONE, PLAY, BLEND, QUEUE,
	}
	public struct ClipQueue {
		public int clipIndex;
		public float blendTime;
	}

	private float EPSILON = 1E-05F;
	
	private Animator animator = null;
	private AnimationClip[] animations = null;
	
	private PlayableGraph graph;
	private AnimationPlayableOutput output;
	private AnimationMixerPlayable mixer;
	private AnimationClipPlayable[] clips = null;
	private int actInputIndex = 1;
	private int clipIndex = -1;
	private float remainingTime = 0f;
	private float targetTime = 0f;
	private float clipDuration = 0f;
	private float addTime = 0f;
	private WrapMode wrapMode = WrapMode.Clamp;
	private STATE state = STATE.NONE;
	private Queue<ClipQueue> clipQueue = null;


	public bool initialized { get; private set; }
	public bool isPlaying { get { return this.state != STATE.NONE; } }
	

	/// <summary>
	/// start-up
	/// </summary>
	public void Initialize(Animator animator, AnimationClip[] animations, int maxQueue) {
		if (this.initialized)
			return;
		
		Debug.Assert(animations != null && animations[0] != null, "No Animation Clips !");
		Debug.Assert(animator != null, "No Animator !");
		Debug.Assert(animator.runtimeAnimatorController == null, "AnimationController is set......");

		this.animator = animator;
		this.animations = animations;

		this.graph = PlayableGraph.Create("Animation Player");
		this.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
		//this.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime); // graph.Evaluate is called at PreLateUpdate when you use this
		this.output = AnimationPlayableOutput.Create(this.graph, this.animator.name, this.animator);
		this.mixer = AnimationMixerPlayable.Create(this.graph, 2, true); // You need 2 channel for blending clip
		this.output.SetSourcePlayable(this.mixer);

		this.clips = new AnimationClipPlayable[this.animations.Length];
		for (int i = 0; i < this.animations.Length; ++i) {
			this.clips[i] = AnimationClipPlayable.Create(this.graph, this.animations[i]);
			this.clips[i].SetApplyFootIK(false);
			this.clips[i].SetApplyPlayableIK(false);
		}
		this.clipQueue = new Queue<ClipQueue>(maxQueue);

		this.initialized = true;
	}

	/// <summary>
	/// shut-down
	/// </summary>
	public void Release() {
		if (this.graph.IsValid())
			this.graph.Destroy();

		this.animator = null;
		this.clips = null;
		this.initialized = false;
	}

	/// <summary>
	/// calculate animation
	/// </summary>
	/// <param name="elapsedTime">sec per frame</param>
	public void Proc(float elapsedTime) {
		Debug.Assert(this.initialized, "SimplestAnimation don't be initialized !");
		
		elapsedTime += this.addTime;
		this.addTime = 0f;

		switch (this.state) {
			case STATE.NONE:
				return; // do not call evaluate
			case STATE.PLAY:
				if (this.wrapMode == WrapMode.Loop)
					break;
				if ((float)this.clips[this.clipIndex].GetTime() < this.clipDuration)
					break;

				//this.graph.Stop(); // need when you don't use ManualMode
				this.state = STATE.NONE;
				break;
			case STATE.BLEND:
				this.remainingTime -= elapsedTime;
				if (this.remainingTime > EPSILON) {
					int nextIndex = this.actInputIndex ^ 0x01;
					float now = this.remainingTime / this.targetTime;
					float next = 1f - now;
					this.mixer.SetInputWeight(this.actInputIndex, now);
					this.mixer.SetInputWeight(nextIndex, next);
				} else {
					this.mixer.GetInput(this.actInputIndex).SetSpeed(1f); // revert setting
					this.mixer.DisconnectInput(this.actInputIndex);
					this.actInputIndex ^= 0x01;
					this.mixer.SetInputWeight(this.actInputIndex, 1f);

					if (this.clipQueue.Count > 0) {
						this.clipDuration = this.clipDuration -  this.clipQueue.Peek().blendTime;
						this.state = STATE.QUEUE;
					} else {
						this.state = STATE.PLAY;
					}
				}
				break;
			case STATE.QUEUE:
				if (this.wrapMode == WrapMode.Loop)
					break;
				float passedTime =  (float)this.clips[this.clipIndex].GetTime() - this.clipDuration;
				if (passedTime < 0f)
					break;

				this.NextQueue();
				this.Proc(passedTime);
				return;
		}
		
		this.graph.Evaluate(elapsedTime); // need when you use ManualMode
	}

	/// <summary>
	/// start clip
	/// </summary>
	/// <param name="clipIndex">playing clip No.</param>
	/// <param name="blendTime">time for blending (sec.)</param>
	/// <param name="passedTime">starting time (sec.)</param>
	public void Play(int clipIndex, float blendTime = 0f, float passedTime = 0f) {
		Debug.Assert(this.initialized, "SimplestAnimation don't be initialized !");


		var nextClip = this.clips[clipIndex];
		
		if (blendTime > 0f) {
			if (this.clipIndex == clipIndex) {
				Debug.LogWarning("Same clip is blended. Skip Animation : " + this.animations[clipIndex].name);
				return;
			}

			var nextIndex = this.actInputIndex ^ 0x01;
			this.mixer.GetInput(this.actInputIndex).SetSpeed(0f); // 現在のポーズから即時ブレンド
			this.mixer.DisconnectInput(nextIndex);
			this.mixer.ConnectInput(nextIndex, nextClip, 0);
			this.mixer.SetInputWeight(nextIndex, 0f);
			this.state = STATE.BLEND;
		} else {
			this.actInputIndex = 1;
			this.mixer.DisconnectInput(this.actInputIndex);
			this.mixer.DisconnectInput(this.actInputIndex ^ 0x01);
			this.mixer.ConnectInput(this.actInputIndex, nextClip, 0);
			this.mixer.SetInputWeight(this.actInputIndex, 1f);
			this.state = STATE.PLAY;
		}
		
		this.clipQueue.Clear();
		this.clipIndex = clipIndex;
		this.clipDuration = this.animations[this.clipIndex].length;
		this.wrapMode = this.animations[this.clipIndex].wrapMode;
		nextClip.SetTime(0f);

		this.remainingTime = this.targetTime = blendTime;
		this.addTime = passedTime;

		//this.graph.Play(); // need when you don't use ManualMode
	}

	/// <summary>
	/// queue next clip
	/// </summary>
	/// <param name="clipIndex">playing clip No.</param>
	/// <param name="blendTime">time for blending (sec.)</param>
	public void PlayQueued(int clipIndex, float blendTime = 0f) {
		Debug.Assert(this.initialized, "SimplestAnimation don't be initialized !");

		if (this.state == STATE.NONE || this.wrapMode == WrapMode.Loop) {
			this.Play(clipIndex, blendTime);
			return;
		}

		var queue = new ClipQueue() { clipIndex = clipIndex, blendTime = blendTime };
		this.clipQueue.Enqueue(queue);
		if (this.state == STATE.PLAY)
			this.state = STATE.QUEUE;
	}

	/// <summary>
	/// stop playing animation
	/// </summary>
	public void Stop() {
		Debug.Assert(this.initialized, "SimplestAnimation don't be initialized !");

		this.actInputIndex = 0;
		this.state = STATE.NONE;
		//this.graph.Stop(); // need when you don't use ManualMode
	}

	/// <summary>
	/// finish looped clip
	/// </summary>
	public void EndLoop() {
		this.wrapMode = WrapMode.Clamp;
	}

	/// <summary>
	/// play next queued clip
	/// </summary>
	private void NextQueue() {
		Debug.Assert(this.initialized, "SimplestAnimation don't be initialized !");

		ClipQueue next = this.clipQueue.Dequeue();

		var nextIndex = this.actInputIndex ^ 0x01;
		this.clipIndex = next.clipIndex;
		this.clipDuration = this.animations[next.clipIndex].length;
		this.wrapMode = this.animations[this.clipIndex].wrapMode;
		this.mixer.ConnectInput(nextIndex, this.clips[this.clipIndex], 0);
		this.mixer.SetInputWeight(nextIndex, 0f);
		this.state = STATE.BLEND;

		this.remainingTime = this.targetTime = next.blendTime;
		this.clips[this.clipIndex].SetTime(0f);
	}
}
