﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyright 2015, by Michael Billard (Angel-125)
License: CC BY-NC-SA 4.0
License URL: https://creativecommons.org/licenses/by-nc-sa/4.0/
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace WildBlueIndustries
{
    public class WBIResourceConverter : ModuleResourceConverter
    {
        private const float kminimumSuccess = 80f;
        private const float kCriticalSuccess = 95f;
        private const float kCriticalFailure = 33f;
        private const float kDefaultHoursPerCycle = 1.0f;

        //Result messages for lastAttempt
        protected string attemptCriticalFail = "Critical Failure";
        protected string attemptCriticalSuccess = "Critical Success";
        protected string attemptFail = "Fail";
        protected string attemptSuccess = "Success";

        public static bool showResults = true;

        [KSPField]
        public float minimumSuccess;

        [KSPField]
        public float criticalSuccess;

        [KSPField]
        public float criticalFail;

        [KSPField]
        public double hoursPerCycle;

        [KSPField(isPersistant = true)]
        public double cycleStartTime;

        [KSPField(guiActive = true, guiName = "Progress", isPersistant = true)]
        public string progress;

        [KSPField(guiActive = true, guiName = "Last Attempt", isPersistant = true)]
        public string lastAttempt;

        [KSPField(isPersistant = true)]
        public bool showGUI = true;

        public double elapsedTime;
        protected float totalCrewSkill = -1.0f;
        protected double secondsPerCycle = 0f;

        [KSPEvent(guiName = "Start Converter", guiActive = true)]
        public virtual void StartConverter()
        {
            StartResourceConverter();
            cycleStartTime = Planetarium.GetUniversalTime();
            lastUpdateTime = cycleStartTime;
            elapsedTime = 0.0f;
            Events["StartConverter"].guiActive = false;
            Events["StopConverter"].guiActive = true;
        }

        [KSPEvent(guiName = "Stop Converter", guiActive = true)]
        public virtual void StopConverter()
        {
            StopResourceConverter();
            progress = "None";
            Events["StartConverter"].guiActive = true;
            Events["StopConverter"].guiActive = false;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Events["StartResourceConverter"].guiActive = false;
            Events["StopResourceConverter"].guiActive = false;

            //Setup
            progress = "None";
            if (hoursPerCycle == 0f)
                hoursPerCycle = kDefaultHoursPerCycle;

            Events["StartConverter"].guiName = StartActionName;
            Events["StopConverter"].guiName = StopActionName;
            if (showGUI)
            {
                if (ModuleIsActive())
                {
                    Events["StartConverter"].guiActive = false;
                    Events["StopConverter"].guiActive = true;
                }
                else
                {
                    Events["StartConverter"].guiActive = true;
                    Events["StopConverter"].guiActive = false;
                }
            }

            if (minimumSuccess == 0)
                minimumSuccess = kminimumSuccess;
            if (criticalSuccess == 0)
                criticalSuccess = kCriticalSuccess;
            if (criticalFail == 0)
                criticalFail = kCriticalFailure;
        }

        protected override void PostProcess(ConverterResults result, double deltaTime)
        {
            base.PostProcess(result, deltaTime);
            Events["StartResourceConverter"].guiActive = false;
            Events["StopResourceConverter"].guiActive = false;

            if (FlightGlobals.ready == false)
                return;
            if (HighLogic.LoadedSceneIsFlight == false)
                return;
            if (ModuleIsActive() == false)
                return;
            if (this.part.vessel.IsControllable == false)
            {
                StopConverter();
                return;
            }
            if (hoursPerCycle == 0f)
                return;

            //Calculate the crew skill and seconds of research per cycle.
            //Thes values can change if the player swaps out crew.
            totalCrewSkill = GetTotalCrewSkill();
            secondsPerCycle = GetSecondsPerCycle();

            //Calculate elapsed time
            elapsedTime = Planetarium.GetUniversalTime() - cycleStartTime;

            //Calculate progress
            CalculateProgress();

            //If we've completed our research cycle then perform the analyis.
            int cyclesSinceLastUpdate = Mathf.RoundToInt((float)(elapsedTime / secondsPerCycle));
            int currentCycle;
            for (currentCycle = 0; currentCycle < cyclesSinceLastUpdate; currentCycle++)
            {
                PerformAnalysis();

                //Reset start time
                cycleStartTime = Planetarium.GetUniversalTime();
            }

            //If we're missing resources then stop the converter
            if (result.Status.ToLower().Contains("missing"))
            {
                StopConverter();
                status = result.Status;
            }
        }
        public virtual void SetGuiVisible(bool isVisible)
        {
            Fields["lastAttempt"].guiActive = isVisible;
            Fields["lastAttempt"].guiActiveEditor = isVisible;
            Fields["progress"].guiActive = isVisible;
            Fields["progress"].guiActiveEditor = isVisible;
            Fields["status"].guiActive = isVisible;

            if (isVisible)
            {
                if (ModuleIsActive())
                {
                    Events["StartConverter"].guiActive = false;
                    Events["StartConverter"].guiActiveUnfocused = false;
                    Events["StartConverter"].guiActiveEditor = false;
                    Events["StopConverter"].guiActive = true;
                    Events["StopConverter"].guiActiveUnfocused = true;
                    Events["StopConverter"].guiActiveEditor = true;
                }

                else
                {
                    Events["StartConverter"].guiActive = true;
                    Events["StartConverter"].guiActiveUnfocused = true;
                    Events["StartConverter"].guiActiveEditor = true;
                    Events["StopConverter"].guiActive = false;
                    Events["StopConverter"].guiActiveUnfocused = false;
                    Events["StopConverter"].guiActiveEditor = false;
                }
            }

            else
            {
                Events["StartConverter"].guiActive = false;
                Events["StartConverter"].guiActiveUnfocused = false;
                Events["StartConverter"].guiActiveEditor = false;
                Events["StopConverter"].guiActive = false;
                Events["StopConverter"].guiActiveUnfocused = false;
                Events["StopConverter"].guiActiveEditor = false;
            }
        }

        public virtual void CalculateProgress()
        {
            //Get elapsed time (seconds)
            progress = string.Format("{0:f1}%", ((elapsedTime / secondsPerCycle) * 100));
        }

        public virtual float GetTotalCrewSkill()
        {
            float totalSkillPoints = 0f;

            if (this.part.CrewCapacity == 0)
                return 0f;

            foreach (ProtoCrewMember crewMember in this.part.protoModuleCrew)
            {
                if (crewMember.experienceTrait.TypeName == Specialty)
                    totalSkillPoints += crewMember.experienceTrait.CrewMemberExperienceLevel();
            }

            return totalSkillPoints;
        }

        public virtual double GetSecondsPerCycle()
        {
            return hoursPerCycle * 3600;
        }

        public virtual void PerformAnalysis()
        {
            float analysisRoll = performAnalysisRoll();

            if (analysisRoll <= criticalFail)
                onCriticalFailure();

            else if (analysisRoll >= criticalSuccess)
                onCriticalSuccess();

            else if (analysisRoll >= minimumSuccess)
                onSuccess();

            else
                onFailure();

        }

        protected virtual float performAnalysisRoll()
        {
            float roll = 0.0f;

            //Roll 3d6 to approximate a bell curve, then convert it to a value between 1 and 100.
            roll = UnityEngine.Random.Range(1, 6);
            roll += UnityEngine.Random.Range(1, 6);
            roll += UnityEngine.Random.Range(1, 6);
            roll *= 5.5556f;

            //Factor in crew
            roll += totalCrewSkill;

            //Done
            return roll;
        }

        protected virtual void onCriticalFailure()
        {
            lastAttempt = attemptCriticalFail;
        }

        protected virtual void onCriticalSuccess()
        {
            lastAttempt = attemptCriticalSuccess;
        }

        protected virtual void onFailure()
        {
            lastAttempt = attemptFail;
        }

        protected virtual void onSuccess()
        {
            lastAttempt = attemptSuccess;
        }

        public virtual void Log(object message)
        {
            Debug.Log(this.ClassName + " [" + this.GetInstanceID().ToString("X")
                + "][" + Time.time.ToString("0.0000") + "]: " + message);
        }
    }
}
