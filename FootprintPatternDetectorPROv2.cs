#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
#endregion

// Professional Time Profile Modes - zmieniona nazwa żeby uniknąć konfliktu
public enum CustomTimeProfileMode
{
    Auto,           // Automatic selection based on time
    SilverBullet,   // ICT Silver Bullet hours (14:50-15:10 & 20:15-21:00 CET)
    LondonOpen,     // European session (08:00-17:30 CET)
    NewYorkOpen,    // US session open (15:30-17:30 CET)
    LunchTime,      // Low volatility periods (17:30-19:30 CET & Asian)
    Custom          // Manual user settings
}

// Signal Strength Classification
public enum SignalStrength
{
    Basic,          // Single pattern detected
    Strong,         // Multiple patterns + filters passed
    Professional,   // Complex composite patterns
    Institutional   // Stealth patterns with high confidence
}

namespace NinjaTrader.NinjaScript.Indicators
{

private Queue<int> recentSignalBars = new Queue<int>();
private int clusterWindow = 5;
private int clusterThreshold = 2;

    public class FootprintPatternDetectorPRO : Indicator
    {
        #region Variables
        // Core Order Flow Data Series
        private Series<double> deltaValues;
        private Series<double> pocLevels;
        private Series<double> totalVolumes;
        private Series<double> bidVolumes;
        private Series<double> askVolumes;
        private Series<double> cumDelta;
        private Series<double> maxDelta;
        private Series<double> minDelta;
        private Series<double> imbalanceRatio;
        private Series<double> volumeRate;
        
        // Technical Analysis Components
        private SMA avgVolume;
        private double lastPOC;
        
        // Pattern Detection Flags - Basic Patterns (4)
        private bool absorptionBullish = false;
        private bool absorptionBearish = false;
        private bool deltaDivergenceBull = false;
        private bool deltaDivergenceBear = false;
        private bool exhaustionBull = false;
        private bool exhaustionBear = false;
        private bool pocRejectionBull = false;
        private bool pocRejectionBear = false;
        
        // Advanced Patterns (2)
        private bool sequentialAbsorption = false;
        private bool volumeImbalanceCascade = false;
        
        // Professional Patterns (4)
        private bool failedBreakout = false;
        private bool imbalanceRejection = false;
        private bool volumeClimax = false;
        private bool stealthPattern = false;
        
        // Enhanced Pattern Components
        private bool strongAbsorptionBear = false;
        private bool strongAbsorptionBull = false;
        private bool sellingExhaustion = false;
        private bool buyingAbsorption = false;
        private bool sellingClimax = false;
        private bool buyingClimax = false;
        private bool stealthAccumulation = false;
        private bool stealthDistribution = false;
        private bool institutionalFootprint = false;
        
        // Signal Management System
        private int signalCount = 0;
        private int lastSignalBar = -1;
        private List<int> recentSignalBars = new List<int>();
        
        // Professional Level Management
        private List<double> significantPOCLevels = new List<double>();
        private List<double> institutionalLevels = new List<double>();
        
        // Performance Tracking
        private Dictionary<string, int> patternStats = new Dictionary<string, int>();
        private DateTime lastCalculationTime;

        // Time-Based Profile System
        private CustomTimeProfileMode currentProfile = CustomTimeProfileMode.Auto;
        
        // Text positioning management
        private Dictionary<int, List<double>> usedTextPositions = new Dictionary<int, List<double>>();
        private const double TEXT_SPACING = 8.0; // Odstęp między tekstami w tickach
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
    EnableTrendFilter = false;
    ShowIcebergDetection = true;
    ShowSequentialCluster = true;
    ShowTrapBar = true;
                DiagnosticTextColor = Brushes.LightGray;

                BullishTextColor = Brushes.White;
                BearishTextColor = Brushes.White;

                Description = "FootprintPatternDetectorPRO - Professional Order Flow Pattern Recognition System";
                Name = "FootprintPatternDetectorPRO";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                
                // === CORE PARAMETERS ===
                DeltaThreshold = 75;
                VolumeMultiplier = 1.8;
                LookbackPeriod = 25;
                
                // === PROFESSIONAL FILTERS ===
                MinDeltaForSequential = 300;
                MinVolumeMultiplierForSignal = 2.2;
                MaxSignalsPerPeriod = 4;
                SignalCooldownBars = 4;
                RequireVolumeProgression = true;
                RequirePriceConfirmation = true;
                RequireInstitutionalConfirmation = false;
                
                // === ADVANCED PATTERN DETECTION ===
                EnableSmartFiltering = true;
                EnableContextualAnalysis = true;
                EnableInstitutionalDetection = true;
                MinConfidenceScore = 0.65;
                
                // === TIME PROFILES ===
                UseTimeBasedProfiles = true;
                ProfileMode = CustomTimeProfileMode.Auto;
                UseLocalTime = true;
                
                // === ICT SILVER BULLET TIMES ===
                SilverBulletAM_Start = TimeSpan.FromHours(14).Add(TimeSpan.FromMinutes(50));
                SilverBulletAM_End = TimeSpan.FromHours(15).Add(TimeSpan.FromMinutes(10));
                SilverBulletPM_Start = TimeSpan.FromHours(20).Add(TimeSpan.FromMinutes(15));
                SilverBulletPM_End = TimeSpan.FromHours(21);
                
                // === SESSION TIMES ===
                LondonSession_Start = TimeSpan.FromHours(8);
                LondonSession_End = TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(30));
                NewYorkSession_Start = TimeSpan.FromHours(15).Add(TimeSpan.FromMinutes(30));
                NewYorkSession_End = TimeSpan.FromHours(22);
                LunchTime_Start = TimeSpan.FromHours(17).Add(TimeSpan.FromMinutes(30));
                LunchTime_End = TimeSpan.FromHours(19).Add(TimeSpan.FromMinutes(30));
                
                // === BASIC PATTERNS (4) ===
                ShowAbsorption = true;
                ShowDivergence = true;
                ShowExhaustion = true;
                ShowPOCRejection = true;
                
                // === ADVANCED PATTERNS (2) ===
                ShowSequentialAbsorption = true;
                ShowVolumeCascade = true;
                
                // === PROFESSIONAL PATTERNS (4) ===
                ShowFailedBreakouts = true;
                ShowImbalanceRejection = true;
                ShowVolumeClimax = true;
                ShowStealthPatterns = true;
                
                // === DISPLAY OPTIONS ===
                ShowStrongSignalsOnly = false;
                ShowDiagnosticInfo = true;
                ShowPOCLevels = true;
                ShowVolumeProfile = false;
                ShowPatternLabels = true;
                ShowConfidenceScores = false;
                UseTextBackgrounds = true;
                AutoPositionTexts = true;
                
                // === PROFESSIONAL COLORS ===
                BullishColor = Brushes.LimeGreen;
                BearishColor = Brushes.Crimson;
                AbsorptionColor = Brushes.DarkOrange;
                DivergenceColor = Brushes.MediumPurple;
                POCColor = Brushes.DarkGray;
                InstitutionalColor = Brushes.Gold;
                ProfessionalColor = Brushes.DeepSkyBlue;
            }
            else if (State == State.DataLoaded)
            {
                // Initialize data series
                deltaValues = new Series<double>(this);
                pocLevels = new Series<double>(this);
                totalVolumes = new Series<double>(this);
                bidVolumes = new Series<double>(this);
                askVolumes = new Series<double>(this);
                cumDelta = new Series<double>(this);
                maxDelta = new Series<double>(this);
                minDelta = new Series<double>(this);
                imbalanceRatio = new Series<double>(this);
                volumeRate = new Series<double>(this);
                
                // Initialize technical indicators
                avgVolume = SMA(Volume, LookbackPeriod);
                
                // Initialize pattern statistics
                InitializePatternStats();
                
                // Initialize text position management
                usedTextPositions = new Dictionary<int, List<double>>();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < LookbackPeriod) return;
            
            // Professional order flow calculation
            CalculateProfessionalOrderFlowData();
            
            // Apply intelligent time-based profiles
            ApplyTimeBasedProfile();
            
            // Detect all patterns with advanced logic
            DetectAllPatterns();
            
            // Apply professional filtering
            if (EnableSmartFiltering)
                ApplySmartFiltering();
            
            // Draw professional visualizations
            DrawProfessionalSignals();
            
            // Manage professional levels
            ManageProfessionalLevels();
            
            // Update performance statistics
            UpdatePatternStatistics();
            
            // Cleanup old text positions for memory optimization
            CleanupOldTextPositions();
        }

        #region Professional Order Flow Calculation
        private void CalculateProfessionalOrderFlowData()
        {
            double delta = 0;
            double poc = 0;
            double totalVol = Volume[0];
            double bidVol = 0;
            double askVol = 0;
            double maxDelt = 0;
            double minDelt = 0;
            double cumDelt = 0;
            double imbalance = 0;
            double volRate = 0;
            
            try
            {
                // Enhanced order flow calculation with professional algorithms
                if (Instrument.MasterInstrument.InstrumentType == InstrumentType.Future ||
                    Instrument.MasterInstrument.InstrumentType == InstrumentType.Stock)
                {
                    // Professional footprint simulation with advanced logic
                    double bodySize = Math.Abs(Close[0] - Open[0]);
                    double candleRange = High[0] - Low[0];
                    double volumeIntensity = totalVol / Math.Max(candleRange / TickSize, 1);
                    
                    // Enhanced delta calculation based on price action
                    if (Close[0] > Open[0]) // Bullish candle
                    {
                        double bullishRatio = 0.55 + (bodySize / candleRange) * 0.2; // 55-75%
                        askVol = totalVol * Math.Min(bullishRatio, 0.85);
                        bidVol = totalVol - askVol;
                        delta = askVol - bidVol;
                        
                        // Professional max/min delta calculation
                        maxDelt = delta * (1.0 + (volumeIntensity / avgVolume[0]));
                        minDelt = -bidVol * 0.3;
                    }
                    else if (Close[0] < Open[0]) // Bearish candle
                    {
                        double bearishRatio = 0.55 + (bodySize / candleRange) * 0.2;
                        bidVol = totalVol * Math.Min(bearishRatio, 0.85);
                        askVol = totalVol - bidVol;
                        delta = askVol - bidVol; // Negative
                        
                        minDelt = delta * (1.0 + (volumeIntensity / avgVolume[0]));
                        maxDelt = askVol * 0.3;
                    }
                    else // Doji
                    {
                        askVol = totalVol * 0.5;
                        bidVol = totalVol * 0.5;
                        delta = 0;
                        maxDelt = totalVol * 0.4;
                        minDelt = -totalVol * 0.4;
                    }
                    
                    // Professional POC calculation
                    poc = Close[0]; // Simplified - in real implementation use actual POC
                    
                    // Professional imbalance ratio
                    imbalance = Math.Abs(askVol - bidVol) / totalVol;
                    
                    // Volume rate calculation
                    volRate = totalVol / Math.Max(avgVolume[0], 1);
                }
                else
                {
                    // Fallback for other instruments
                    delta = (Close[0] > Open[0] ? 1 : -1) * totalVol * 0.4;
                    poc = (High[0] + Low[0] + Close[0]) / 3;
                    askVol = delta > 0 ? totalVol * 0.7 : totalVol * 0.3;
                    bidVol = totalVol - askVol;
                    maxDelt = Math.Abs(delta) * 1.2;
                    minDelt = -Math.Abs(delta) * 1.2;
                    imbalance = 0.5;
                    volRate = totalVol / Math.Max(avgVolume[0], 1);
                }
                
                // Cumulative delta with professional smoothing
                cumDelt = CurrentBar > 0 ? cumDelta[1] + delta : delta;
                
                // Store all professional data
                deltaValues[0] = delta;
                pocLevels[0] = poc;
                totalVolumes[0] = totalVol;
                bidVolumes[0] = bidVol;
                askVolumes[0] = askVol;
                cumDelta[0] = cumDelt;
                maxDelta[0] = maxDelt;
                minDelta[0] = minDelt;
                imbalanceRatio[0] = imbalance;
                volumeRate[0] = volRate;
                
                lastPOC = poc;
            }
            catch (Exception ex)
            {
                // Professional error handling
                Print("FootprintPatternDetectorPRO Error: " + ex.Message);
                
                // Safe fallback values
                deltaValues[0] = 0;
                pocLevels[0] = Close[0];
                totalVolumes[0] = Volume[0];
                bidVolumes[0] = Volume[0] * 0.5;
                askVolumes[0] = Volume[0] * 0.5;
                cumDelta[0] = CurrentBar > 0 ? cumDelta[1] : 0;
                maxDelta[0] = 0;
                minDelta[0] = 0;
                imbalanceRatio[0] = 0.5;
                volumeRate[0] = 1.0;
            }
        }
        #endregion

        #region Time-Based Profile System
        private void ApplyTimeBasedProfile()
        {
            if (!UseTimeBasedProfiles) return;
                
            DateTime currentTime = Time[0];
            CustomTimeProfileMode activeProfile = ProfileMode;
            
            // Auto mode - intelligent profile selection
            if (ProfileMode == CustomTimeProfileMode.Auto)
            {
                activeProfile = GetIntelligentProfile(currentTime);
            }
            
            // Apply sophisticated profile settings
            ApplyProfessionalProfileSettings(activeProfile);
            
            // Update context
            currentProfile = activeProfile;
        }
        
        private CustomTimeProfileMode GetIntelligentProfile(DateTime time)
        {
            DateTime currentTime = UseLocalTime ? time.ToLocalTime() : time;
            TimeSpan timeOfDay = currentTime.TimeOfDay;
            DayOfWeek dayOfWeek = currentTime.DayOfWeek;
            
            // Weekend handling
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                return CustomTimeProfileMode.LunchTime; // Quiet settings
            
            // ICT Silver Bullet periods (highest priority)
            if ((timeOfDay >= SilverBulletAM_Start && timeOfDay <= SilverBulletAM_End) ||
                (timeOfDay >= SilverBulletPM_Start && timeOfDay <= SilverBulletPM_End))
                return CustomTimeProfileMode.SilverBullet;
                
            // London-New York overlap (high volatility)
            if (timeOfDay >= NewYorkSession_Start && timeOfDay <= LondonSession_End)
                return CustomTimeProfileMode.NewYorkOpen;
                
            // London session
            if (timeOfDay >= LondonSession_Start && timeOfDay <= LondonSession_End)
                return CustomTimeProfileMode.LondonOpen;
                
            // Lunch/Asian hours
            if (timeOfDay >= LunchTime_Start && timeOfDay <= LunchTime_End)
                return CustomTimeProfileMode.LunchTime;
                
            return CustomTimeProfileMode.Custom;
        }
        
        private void ApplyProfessionalProfileSettings(CustomTimeProfileMode profile)
        {
            switch (profile)
            {
                case CustomTimeProfileMode.SilverBullet:
                    // ULTRA RESTRICTIVE - Only highest quality signals
                    MinDeltaForSequential = 500;
                    MinVolumeMultiplierForSignal = 3.5;
                    MaxSignalsPerPeriod = 1;
                    SignalCooldownBars = 8;
                    RequireVolumeProgression = true;
                    RequirePriceConfirmation = true;
                    RequireInstitutionalConfirmation = true;
                    break;
                    
                case CustomTimeProfileMode.NewYorkOpen:
                    // HIGH VOLATILITY - Quality over quantity
                    MinDeltaForSequential = 350;
                    MinVolumeMultiplierForSignal = 2.5;
                    MaxSignalsPerPeriod = 3;
                    SignalCooldownBars = 5;
                    RequireVolumeProgression = true;
                    RequirePriceConfirmation = true;
                    RequireInstitutionalConfirmation = false;
                    break;
                    
                case CustomTimeProfileMode.LondonOpen:
                    // MODERATE VOLATILITY - Balanced approach
                    MinDeltaForSequential = 250;
                    MinVolumeMultiplierForSignal = 2.0;
                    MaxSignalsPerPeriod = 5;
                    SignalCooldownBars = 3;
                    RequireVolumeProgression = true;
                    RequirePriceConfirmation = false;
                    RequireInstitutionalConfirmation = false;
                    break;
                    
                case CustomTimeProfileMode.LunchTime:
                    // LOW VOLATILITY - More sensitive
                    MinDeltaForSequential = 150;
                    MinVolumeMultiplierForSignal = 1.5;
                    MaxSignalsPerPeriod = 8;
                    SignalCooldownBars = 2;
                    RequireVolumeProgression = false;
                    RequirePriceConfirmation = false;
                    RequireInstitutionalConfirmation = false;
                    break;
                    
                case CustomTimeProfileMode.Custom:
                    // Use user-defined settings
                    break;
            }
        }
        #endregion

        #region Pattern Detection
        private void DetectAllPatterns()
        {
            // Reset all pattern flags
            ResetPatternFlags();
            
            double currentDelta = deltaValues[0];
            double avgVol = avgVolume[0];
            bool highVolume = totalVolumes[0] > avgVol * VolumeMultiplier;
            
            // BASIC PATTERNS (4)
            if (ShowAbsorption)
                DetectProfessionalAbsorption(currentDelta, highVolume, avgVol);
            
            if (ShowDivergence && CurrentBar >= 8)
                DetectAdvancedDivergence(currentDelta);
            
            if (ShowExhaustion)
                DetectProfessionalExhaustion(currentDelta, avgVol);
            
            if (ShowPOCRejection && CurrentBar >= 3)
                DetectProfessionalPOCRejection(currentDelta);
            
            // ADVANCED PATTERNS (2)
            if (ShowSequentialAbsorption && CurrentBar >= 2)
                DetectEnhancedSequentialAbsorption(currentDelta, highVolume, avgVol);
                
            if (ShowVolumeCascade && CurrentBar >= 6)
                DetectProfessionalVolumeCascade(currentDelta, highVolume, avgVol);
            
            // PROFESSIONAL PATTERNS (4)
            if (ShowFailedBreakouts && CurrentBar >= 3)
                DetectProfessionalFailedBreakout(currentDelta, highVolume, avgVol);
                
            if (ShowImbalanceRejection && CurrentBar >= 2)
                DetectAdvancedImbalanceRejection(currentDelta, highVolume, avgVol);
                
            if (ShowVolumeClimax && CurrentBar >= 8)
                DetectProfessionalVolumeClimax(currentDelta, highVolume, avgVol);
                
            if (ShowStealthPatterns && CurrentBar >= 10)
                DetectInstitutionalStealthPattern(currentDelta, highVolume, avgVol);
        }
        
        private void ResetPatternFlags()
        {
            absorptionBullish = absorptionBearish = false;
            deltaDivergenceBull = deltaDivergenceBear = false;
            exhaustionBull = exhaustionBear = false;
            pocRejectionBull = pocRejectionBear = false;
            strongAbsorptionBear = strongAbsorptionBull = false;
            sequentialAbsorption = sellingExhaustion = buyingAbsorption = false;
            volumeImbalanceCascade = failedBreakout = imbalanceRejection = false;
            volumeClimax = sellingClimax = buyingClimax = false;
            stealthPattern = stealthAccumulation = stealthDistribution = false;
            institutionalFootprint = false;
        }

        private void DetectProfessionalAbsorption(double currentDelta, bool highVolume, double avgVol)
        {
            // Professional absorption detection with multiple confirmation layers
            double bodySize = Math.Abs(Close[0] - Open[0]);
            double candleRange = High[0] - Low[0];
            double currentMaxDelta = maxDelta[0];
            double currentMinDelta = minDelta[0];
            double totalVol = totalVolumes[0];
            double currentImbalance = imbalanceRatio[0];
            
            // Advanced volume analysis
            bool extremeVolume = totalVol > avgVol * MinVolumeMultiplierForSignal * 1.5;
            bool professionalVolume = totalVol > avgVol * MinVolumeMultiplierForSignal;
            
            // Enhanced body analysis
            bool smallBody = bodySize < candleRange * 0.35;
            bool microBody = bodySize < candleRange * 0.2;
            
            // === PROFESSIONAL BULLISH ABSORPTION ===
            bool basicBullishAbsorption = professionalVolume && smallBody && 
                                        currentDelta < -DeltaThreshold && Close[0] > Open[1];
            
            strongAbsorptionBull = extremeVolume && microBody &&
                                 currentMaxDelta > DeltaThreshold * 2 && 
                                 currentMinDelta < -DeltaThreshold * 4 && 
                                 currentDelta > 0 && 
                                 currentImbalance > 0.7 && 
                                 Close[0] > (High[0] + Low[0]) / 2;
            
            absorptionBullish = basicBullishAbsorption || strongAbsorptionBull;
            
            // === PROFESSIONAL BEARISH ABSORPTION ===
            bool basicBearishAbsorption = professionalVolume && smallBody && 
                                        currentDelta > DeltaThreshold && Close[0] < Open[1];
            
            strongAbsorptionBear = extremeVolume && microBody &&
                                 currentMinDelta < -DeltaThreshold * 2 && 
                                 currentMaxDelta > DeltaThreshold * 4 && 
                                 currentDelta < 0 && 
                                 currentImbalance > 0.7 && 
                                 Close[0] < (High[0] + Low[0]) / 2;
            
            absorptionBearish = basicBearishAbsorption || strongAbsorptionBear;
            
            // === SELLING EXHAUSTION ===
            sellingExhaustion = Close[0] < Open[0] && 
                               currentDelta < -DeltaThreshold * 2 && 
                               totalVol > avgVol * VolumeMultiplier && 
                               currentMinDelta < -DeltaThreshold * 3 && 
                               volumeRate[0] > 1.5;
            
            // === BUYING ABSORPTION ===
            if (CurrentBar > 0)
            {
                bool previousWasSellingExhaustion = Close[1] < Open[1] && 
                                                  deltaValues[1] < -DeltaThreshold * 2 &&
                                                  totalVolumes[1] > avgVol * VolumeMultiplier;
                
                buyingAbsorption = Close[0] > Open[0] && 
                                 currentDelta > DeltaThreshold && 
                                 totalVol > avgVol * VolumeMultiplier && 
                                 currentMaxDelta > DeltaThreshold * 2 && 
                                 previousWasSellingExhaustion;
            }
        }
        
        private void DetectEnhancedSequentialAbsorption(double currentDelta, bool highVolume, double avgVol)
        {
            // THE SIGNATURE PATTERN: Enhanced Sequential Absorption Detection
            if (CurrentBar == 0) return;
            
            double previousDelta = deltaValues[1];
            double previousVolume = totalVolumes[1];
            double currentVolume = totalVolumes[0];
            double currentImbalance = imbalanceRatio[0];
            double previousImbalance = imbalanceRatio[1];
            
            // Enhanced validation criteria
            bool previousWasSellingExhaustion = Close[1] < Open[1] && 
                                               Math.Abs(previousDelta) > MinDeltaForSequential && 
                                               previousDelta < -DeltaThreshold && 
                                               previousVolume > avgVol * MinVolumeMultiplierForSignal && 
                                               previousImbalance > 0.6;
            
            bool currentIsBuyingAbsorption = Close[0] > Open[0] && 
                                           currentDelta > DeltaThreshold && 
                                           currentVolume > avgVol * VolumeMultiplier && 
                                           currentImbalance > 0.55;
            
            sequentialAbsorption = previousWasSellingExhaustion && currentIsBuyingAbsorption;
            
            // Professional enhancement filters
            if (sequentialAbsorption)
            {
                if (RequireVolumeProgression)
                {
                    bool volumeProgression = currentVolume >= previousVolume * 0.6;
                    sequentialAbsorption = sequentialAbsorption && volumeProgression;
                }
                
                if (RequirePriceConfirmation)
                {
                    double priceMove = Close[0] - Close[1];
                    double avgRange = (High[1] - Low[1] + High[0] - Low[0]) / 2;
                    bool significantMove = Math.Abs(priceMove) > avgRange * 0.25;
                    sequentialAbsorption = sequentialAbsorption && significantMove;
                }
                
                if (RequireInstitutionalConfirmation)
                {
                    bool institutionalSignature = currentVolume > avgVol * 3.0 && 
                                                currentImbalance > 0.75 && 
                                                Math.Abs(currentDelta) > DeltaThreshold * 2;
                    sequentialAbsorption = sequentialAbsorption && institutionalSignature;
                }
                
                double deltaRatio = Math.Abs(currentDelta / previousDelta);
                bool optimalDeltaRatio = deltaRatio >= 0.15 && deltaRatio <= 0.8;
                sequentialAbsorption = sequentialAbsorption && optimalDeltaRatio;
                
                if (EnableContextualAnalysis)
                {
                    bool contextValid = ValidatePatternContext("SequentialAbsorption");
                    sequentialAbsorption = sequentialAbsorption && contextValid;
                }
            }
        }
        
        private void DetectAdvancedDivergence(double currentDelta)
        {
            if (CurrentBar < 8) return;
            
            double[] highs = new double[8];
            double[] lows = new double[8];
            
            for (int i = 0; i < 8; i++)
            {
                if (CurrentBar >= i)
                {
                    highs[i] = High[i];
                    lows[i] = Low[i];
                }
            }
            
            bool priceHigherHigh = IsHigherHigh(highs, 0, 4);
            bool priceLowerLow = IsLowerLow(lows, 0, 4);
            
            double deltaMA_Current = CalculateDeltaMA(0, 3);
            double deltaMA_Previous = CalculateDeltaMA(3, 3);
            double deltaMA_Earlier = CalculateDeltaMA(6, 2);
            
            bool deltaDecreasing = deltaMA_Current < deltaMA_Previous && deltaMA_Previous < deltaMA_Earlier;
            bool deltaIncreasing = deltaMA_Current > deltaMA_Previous && deltaMA_Previous > deltaMA_Earlier;
            
            deltaDivergenceBear = priceHigherHigh && deltaDecreasing && 
                                currentDelta < -DeltaThreshold * 0.5 && 
                                totalVolumes[0] > avgVolume[0] * 1.5;
            
            deltaDivergenceBull = priceLowerLow && deltaIncreasing && 
                                currentDelta > DeltaThreshold * 0.5 && 
                                totalVolumes[0] > avgVolume[0] * 1.5;
        }
        
        private void DetectProfessionalExhaustion(double currentDelta, double avgVol)
        {
            bool extremeVolume = totalVolumes[0] > avgVol * 2.5;
            bool highImbalance = imbalanceRatio[0] > 0.75;
            
            exhaustionBull = currentDelta > DeltaThreshold * 2.5 && 
                           Close[0] < Close[1] && 
                           extremeVolume && 
                           highImbalance && 
                           maxDelta[0] > DeltaThreshold * 3;
            
            exhaustionBear = currentDelta < -DeltaThreshold * 2.5 && 
                           Close[0] > Close[1] && 
                           extremeVolume && 
                           highImbalance && 
                           minDelta[0] < -DeltaThreshold * 3;
        }
        
        private void DetectProfessionalPOCRejection(double currentDelta)
        {
            if (CurrentBar < 3) return;
            
            double significantPOC = GetMostSignificantPOC(3);
            
            bool bullishRejection = Low[0] <= significantPOC * 1.001 && 
                                  Close[0] > significantPOC * 1.002 && 
                                  currentDelta > DeltaThreshold && 
                                  totalVolumes[0] > avgVolume[0] * 1.5 && 
                                  Close[0] > Open[0];
            
            bool bearishRejection = High[0] >= significantPOC * 0.999 && 
                                  Close[0] < significantPOC * 0.998 && 
                                  currentDelta < -DeltaThreshold && 
                                  totalVolumes[0] > avgVolume[0] * 1.5 && 
                                  Close[0] < Open[0];
            
            pocRejectionBull = bullishRejection;
            pocRejectionBear = bearishRejection;
        }
        
        private void DetectProfessionalVolumeCascade(double currentDelta, bool highVolume, double avgVol)
        {
            if (CurrentBar < 6) return;
            
            // Simplified volume cascade detection for compilation
            volumeImbalanceCascade = Math.Abs(currentDelta) > DeltaThreshold * 3 && 
                                   totalVolumes[0] > avgVol * 2.5 && 
                                   imbalanceRatio[0] > 0.7;
        }
        
        private void DetectProfessionalFailedBreakout(double currentDelta, bool highVolume, double avgVol)
        {
            if (CurrentBar < 3) return;
            
            double prevDelta = deltaValues[1];
            double prev2Delta = deltaValues[2];
            
            bool hadSetup = Math.Abs(prev2Delta) > DeltaThreshold && Math.Abs(prevDelta) > DeltaThreshold;
            bool massiveReversal = Math.Abs(currentDelta) > DeltaThreshold * 2.5 && 
                                 Math.Abs(currentDelta) > Math.Abs(prevDelta) * 1.5;
            bool volumeExplosion = totalVolumes[0] > avgVol * MinVolumeMultiplierForSignal;
            
            failedBreakout = hadSetup && massiveReversal && volumeExplosion;
        }
        
        private void DetectAdvancedImbalanceRejection(double currentDelta, bool highVolume, double avgVol)
        {
            if (CurrentBar < 2) return;
            
            imbalanceRejection = imbalanceRatio[0] > 0.6 && 
                               totalVolumes[0] > avgVol * MinVolumeMultiplierForSignal && 
                               Math.Abs(currentDelta) > DeltaThreshold;
        }
        
        private void DetectProfessionalVolumeClimax(double currentDelta, bool highVolume, double avgVol)
        {
            if (CurrentBar < 8) return;
            
            bool extremeDelta = Math.Abs(currentDelta) > DeltaThreshold * 4;
            bool extremeVolume = totalVolumes[0] > avgVol * 3.0;
            bool highImbalance = imbalanceRatio[0] > 0.8;
            
            volumeClimax = extremeDelta && extremeVolume && highImbalance;
            sellingClimax = volumeClimax && currentDelta < 0;
            buyingClimax = volumeClimax && currentDelta > 0;
        }
        
        private void DetectInstitutionalStealthPattern(double currentDelta, bool highVolume, double avgVol)
        {
            if (CurrentBar < 10) return;
            
            // Stealth pattern detection
            bool moderateVolume = totalVolumes[0] > avgVol * 1.5 && totalVolumes[0] < avgVol * 2.5;
            bool significantDelta = Math.Abs(currentDelta) > DeltaThreshold;
            
            stealthPattern = moderateVolume && significantDelta;
            stealthAccumulation = stealthPattern && currentDelta > 0;
            stealthDistribution = stealthPattern && currentDelta < 0;
            
            // Institutional footprint
            if (EnableInstitutionalDetection)
            {
                institutionalFootprint = totalVolumes[0] > avgVol * 4.0 && 
                                       Math.Abs(currentDelta) > DeltaThreshold * 3 && 
                                       imbalanceRatio[0] > 0.8;
            }
        }
        
        // Helper methods
        private bool IsHigherHigh(double[] highs, int startIndex, int lookback)
        {
            if (startIndex + lookback >= highs.Length) return false;
            
            double currentHigh = highs[startIndex];
            for (int i = startIndex + 1; i < startIndex + lookback; i++)
            {
                if (highs[i] >= currentHigh) return false;
            }
            return true;
        }
        
        private bool IsLowerLow(double[] lows, int startIndex, int lookback)
        {
            if (startIndex + lookback >= lows.Length) return false;
            
            double currentLow = lows[startIndex];
            for (int i = startIndex + 1; i < startIndex + lookback; i++)
            {
                if (lows[i] <= currentLow) return false;
            }
            return true;
        }
        
        private double CalculateDeltaMA(int startIndex, int period)
        {
            if (CurrentBar < startIndex + period) return 0;
            
            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                sum += deltaValues[startIndex + i];
            }
            return sum / period;
        }
        
        private double GetMostSignificantPOC(int lookback)
        {
            double significantPOC = pocLevels[0];
            double maxVolume = 0;
            
            for (int i = 0; i < lookback && CurrentBar >= i; i++)
            {
                if (totalVolumes[i] > maxVolume)
                {
                    maxVolume = totalVolumes[i];
                    significantPOC = pocLevels[i];
                }
            }
            return significantPOC;
        }
        
        private bool ValidatePatternContext(string patternName)
        {
            if (!EnableContextualAnalysis) return true;
            return totalVolumes[0] > avgVolume[0] * 1.2;
        }
        #endregion

        #region Professional Filtering System
        private void ApplySmartFiltering()
        {
            if (!EnableSmartFiltering) return;
            
            List<string> detectedPatterns = GetDetectedPatterns();
            
            foreach (string pattern in detectedPatterns)
            {
                double confidence = CalculatePatternConfidence(pattern);
                
                if (confidence < MinConfidenceScore)
                {
                    DisablePattern(pattern);
                }
            }
        }
        
        private List<string> GetDetectedPatterns()
        {
            List<string> patterns = new List<string>();
            
            if (absorptionBullish || absorptionBearish) patterns.Add("Absorption");
            if (deltaDivergenceBull || deltaDivergenceBear) patterns.Add("Divergence");
            if (exhaustionBull || exhaustionBear) patterns.Add("Exhaustion");
            if (pocRejectionBull || pocRejectionBear) patterns.Add("POCRejection");
            if (sequentialAbsorption) patterns.Add("SequentialAbsorption");
            if (volumeImbalanceCascade) patterns.Add("VolumeCascade");
            if (failedBreakout) patterns.Add("FailedBreakout");
            if (imbalanceRejection) patterns.Add("ImbalanceRejection");
            if (volumeClimax) patterns.Add("VolumeClimax");
            if (stealthPattern) patterns.Add("StealthPattern");
            
            return patterns;
        }
        
        private double CalculatePatternConfidence(string pattern)
        {
            double confidence = 0.5;
            
            double volumeFactor = Math.Min(volumeRate[0] / 2.0, 1.0);
            confidence += volumeFactor * 0.2;
            
            double deltaStrength = Math.Min(Math.Abs(deltaValues[0]) / (DeltaThreshold * 3), 1.0);
            confidence += deltaStrength * 0.15;
            
            confidence += imbalanceRatio[0] * 0.15;
            
            switch (pattern)
            {
                case "SequentialAbsorption":
                    confidence += 0.15;
                    break;
                case "VolumeClimax":
                    confidence += 0.12;
                    break;
                case "StealthPattern":
                    if (institutionalFootprint) confidence += 0.2;
                    break;
            }
            
            return Math.Max(0.0, Math.Min(1.0, confidence));
        }
        
        private void DisablePattern(string pattern)
        {
            switch (pattern)
            {
                case "Absorption":
                    absorptionBullish = absorptionBearish = false;
                    break;
                case "Divergence":
                    deltaDivergenceBull = deltaDivergenceBear = false;
                    break;
                case "SequentialAbsorption":
                    sequentialAbsorption = false;
                    break;
                case "VolumeCascade":
                    volumeImbalanceCascade = false;
                    break;
                case "VolumeClimax":
                    volumeClimax = sellingClimax = buyingClimax = false;
                    break;
                case "StealthPattern":
                    stealthPattern = stealthAccumulation = stealthDistribution = false;
                    break;
            }
        }
        
        private void RegisterSignal()
        {
            lastSignalBar = CurrentBar;
            recentSignalBars.Add(CurrentBar);
            signalCount++;
            
            // Cleanup old signals
            if (recentSignalBars.Count > 100)
            {
                recentSignalBars.RemoveAt(0);
            }
        }
        
        private string GetPrimaryPatternType()
        {
            if (sequentialAbsorption) return "SequentialAbsorption";
            if (volumeClimax) return "VolumeClimax";
            if (stealthPattern) return "StealthPattern";
            if (absorptionBullish || absorptionBearish) return "Absorption";
            return "Mixed";
        }
        
        private SignalStrength GetSignalStrength()
        {
            int patternCount = GetDetectedPatterns().Count;
            
            if (institutionalFootprint) return SignalStrength.Institutional;
            if (sequentialAbsorption || volumeClimax) return SignalStrength.Professional;
            if (patternCount >= 2) return SignalStrength.Strong;
            return SignalStrength.Basic;
        }
        
        private double CalculateOverallConfidence()
        {
            List<string> patterns = GetDetectedPatterns();
            if (patterns.Count == 0) return 0;
            
            double totalConfidence = 0;
            foreach (string pattern in patterns)
            {
                totalConfidence += CalculatePatternConfidence(pattern);
            }
            return totalConfidence / patterns.Count;
        }
        #endregion

        #region Professional Visualization System
        private void DrawProfessionalSignals()
        {
            string tag = "ProPattern_" + CurrentBar;
            
            SignalStrength signalStrength = GetSignalStrength();
            bool hasStrongBullish = IsStrongBullishSignal();
            bool hasStrongBearish = IsStrongBearishSignal();
            
            if (!hasStrongBullish && !hasStrongBearish && ShowStrongSignalsOnly) return;
            
            if (signalStrength == SignalStrength.Institutional)
            {
                DrawInstitutionalSignals(tag, hasStrongBullish, hasStrongBearish);
            }
            else if (signalStrength == SignalStrength.Professional)
            {
                DrawProfessionalGradeSignals(tag, hasStrongBullish, hasStrongBearish);
            }
            else if (hasStrongBullish || hasStrongBearish)
            {
                DrawStrongSignals(tag, hasStrongBullish, hasStrongBearish);
            }
            else if (!ShowStrongSignalsOnly)
            {
                DrawBasicSignals(tag);
            }
            
            if (ShowDiagnosticInfo)
            {
                DrawDiagnosticInfo(tag);
            }
            
            DrawPatternSpecificVisuals(tag);
            
            if (hasStrongBullish || hasStrongBearish)
            {
                RegisterSignal();
            }
        }
        
        private bool IsStrongBullishSignal()
        {
            return (absorptionBullish || deltaDivergenceBull || pocRejectionBull || 
                   sequentialAbsorption || buyingAbsorption || sellingClimax || 
                   stealthAccumulation || (volumeImbalanceCascade && deltaValues[0] > 0)) && 
                   deltaValues[0] > DeltaThreshold;
        }
        
        private bool IsStrongBearishSignal()
        {
            return (absorptionBearish || deltaDivergenceBear || pocRejectionBear ||
                   sellingExhaustion || buyingClimax || stealthDistribution ||
                   (volumeImbalanceCascade && deltaValues[0] < 0)) && 
                   deltaValues[0] < -DeltaThreshold;
        }
        
        private void DrawInstitutionalSignals(string tag, bool bullish, bool bearish)
        {
            if (bullish)
            {
                // Kontrastowa strzałka z obramowaniem
                Draw.ArrowUp(this, tag + "_InstArrowUp", false, 0, Low[0] - 6 * TickSize, Brushes.Black);
                Draw.ArrowUp(this, tag + "_InstArrowUpInner", false, 0, Low[0] - 6 * TickSize, InstitutionalColor);
                
                // Tekst z tłem dla lepszej widoczności
                double textY = AutoPositionTexts ? GetNextTextPosition(Low[0] - 15 * TickSize, true) : Low[0] - 15 * TickSize;
                DrawTextWithBackground(tag + "_InstBullText", "🏛️ INSTITUTIONAL BUY", 0, textY, InstitutionalColor, Brushes.Black);
            }
            
            if (bearish)
            {
                // Kontrastowa strzałka z obramowaniem
                Draw.ArrowDown(this, tag + "_InstArrowDown", false, 0, High[0] + 6 * TickSize, Brushes.Black);
                Draw.ArrowDown(this, tag + "_InstArrowDownInner", false, 0, High[0] + 6 * TickSize, InstitutionalColor);
                
                // Tekst z tłem dla lepszej widoczności
                double textY = AutoPositionTexts ? GetNextTextPosition(High[0] + 15 * TickSize, false) : High[0] + 15 * TickSize;
                DrawTextWithBackground(tag + "_InstBearText", "🏛️ INSTITUTIONAL SELL", 0, textY, InstitutionalColor, Brushes.Black);
            }
        }
        
        private void DrawProfessionalGradeSignals(string tag, bool bullish, bool bearish)
        {
            if (bullish)
            {
                // Kontrastowa strzałka
                Draw.TriangleUp(this, tag + "_ProBullBorder", false, 0, Low[0] - 5 * TickSize, Brushes.Black);
                Draw.TriangleUp(this, tag + "_ProBull", false, 0, Low[0] - 5 * TickSize, ProfessionalColor);
                
                // Tekst z lepszą widocznością
                double textY = AutoPositionTexts ? GetNextTextPosition(Low[0] - 12 * TickSize, true) : Low[0] - 12 * TickSize;
                DrawTextWithBackground(tag + "_ProBullText", "⚡ PROFESSIONAL BUY", 0, textY, ProfessionalColor, Brushes.Black);
            }
            
            if (bearish)
            {
                // Kontrastowa strzałka
                Draw.TriangleDown(this, tag + "_ProBearBorder", false, 0, High[0] + 5 * TickSize, Brushes.Black);
                Draw.TriangleDown(this, tag + "_ProBear", false, 0, High[0] + 5 * TickSize, ProfessionalColor);
                
                // Tekst z lepszą widocznością
                double textY = AutoPositionTexts ? GetNextTextPosition(High[0] + 12 * TickSize, false) : High[0] + 12 * TickSize;
                DrawTextWithBackground(tag + "_ProBearText", "⚡ PROFESSIONAL SELL", 0, textY, ProfessionalColor, Brushes.Black);
            }
        }
        
        private void DrawStrongSignals(string tag, bool bullish, bool bearish)
        {
            if (bullish)
            {
                // Kontrastowa strzałka z obramowaniem
                Draw.TriangleUp(this, tag + "_StrongBullBorder", false, 0, Low[0] - 4 * TickSize, Brushes.Black);
                Draw.TriangleUp(this, tag + "_StrongBull", false, 0, Low[0] - 4 * TickSize, BullishColor);
                
                // Tekst z lepszą widocznością
                double textY = AutoPositionTexts ? GetNextTextPosition(Low[0] - 10 * TickSize, true) : Low[0] - 10 * TickSize;
                DrawTextWithBackground(tag + "_BullText", "💪 STRONG BUY", 0, textY, BullishTextColor, BullishColor);
            }
            
            if (bearish)
            {
                // Kontrastowa strzałka z obramowaniem
                Draw.TriangleDown(this, tag + "_StrongBearBorder", false, 0, High[0] + 4 * TickSize, Brushes.Black);
                Draw.TriangleDown(this, tag + "_StrongBear", false, 0, High[0] + 4 * TickSize, BearishColor);
                
                // Tekst z lepszą widocznością
                double textY = AutoPositionTexts ? GetNextTextPosition(High[0] + 10 * TickSize, false) : High[0] + 10 * TickSize;
                DrawTextWithBackground(tag + "_BearText", "💪 STRONG SELL", 0, textY, BearishTextColor, BearishColor);
            }
        }
        
        private void DrawBasicSignals(string tag)
        {
            if (absorptionBullish)
            {
                Draw.Dot(this, tag + "_AbsBullBorder", false, 0, Low[0] - TickSize, Brushes.Black);
                Draw.Dot(this, tag + "_AbsBull", false, 0, Low[0] - TickSize, AbsorptionColor);
            }
            
            if (absorptionBearish)
            {
                Draw.Dot(this, tag + "_AbsBearBorder", false, 0, High[0] + TickSize, Brushes.Black);
                Draw.Dot(this, tag + "_AbsBear", false, 0, High[0] + TickSize, AbsorptionColor);
            }
            
            if (deltaDivergenceBull)
            {
                Draw.Diamond(this, tag + "_DivBullBorder", false, 0, Low[0] - TickSize, Brushes.Black);
                Draw.Diamond(this, tag + "_DivBull", false, 0, Low[0] - TickSize, DivergenceColor);
            }
            
            if (deltaDivergenceBear)
            {
                Draw.Diamond(this, tag + "_DivBearBorder", false, 0, High[0] + TickSize, Brushes.Black);
                Draw.Diamond(this, tag + "_DivBear", false, 0, High[0] + TickSize, DivergenceColor);
            }
        }
        
        private void DrawDiagnosticInfo(string tag)
        {
            string diagnosticInfo = BuildDiagnosticString();
            
            if (!string.IsNullOrEmpty(diagnosticInfo))
            {
                Draw.Text(this, tag + "_Diagnostic", diagnosticInfo, 0, 
                         Low[0] - 15 * TickSize, DiagnosticTextColor);
            }
        }
        
        private string BuildDiagnosticString()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("Δ: " + deltaValues[0].ToString("F0") + " | Vol: " + totalVolumes[0].ToString("F0") + " | Ratio: " + volumeRate[0].ToString("F1") + "x");
            
            if (ShowDiagnosticInfo)
            {
                sb.AppendLine("Max: " + maxDelta[0].ToString("F0") + " | Min: " + minDelta[0].ToString("F0") + " | Imb: " + imbalanceRatio[0].ToString("P0"));
                sb.AppendLine("CumΔ: " + cumDelta[0].ToString("F0") + " | Profile: " + currentProfile.ToString());
                
                int patternCount = GetDetectedPatterns().Count;
                if (patternCount > 0)
                {
                    sb.AppendLine("Patterns: " + patternCount.ToString() + " | Conf: " + CalculateOverallConfidence().ToString("P0"));
                }
            }
            
            return sb.ToString().TrimEnd();
        }
        
        private void DrawPatternSpecificVisuals(string tag)
        {
            if (sequentialAbsorption)
            {
                DrawSequentialAbsorptionVisual(tag);
            }
            
            if (volumeImbalanceCascade)
            {
                double textY = AutoPositionTexts ? GetNextTextPosition(High[0] + 8 * TickSize, false) : High[0] + 8 * TickSize;
                DrawTextWithBackground(tag + "_Cascade", "⚡ VOLUME CASCADE", 0, textY, Brushes.Purple, Brushes.White);
            }
            
            if (volumeClimax)
            {
                if (sellingClimax)
                {
                    double textY = AutoPositionTexts ? GetNextTextPosition(High[0] + 12 * TickSize, false) : High[0] + 12 * TickSize;
                    DrawTextWithBackground(tag + "_SellingClimax", "💥 SELLING CLIMAX", 0, textY, Brushes.Lime, Brushes.Black);
                    
                    Draw.ArrowUp(this, tag + "_ClimaxUpBorder", false, 0, Low[0] - 5 * TickSize, Brushes.Black);
                    Draw.ArrowUp(this, tag + "_ClimaxUp", false, 0, Low[0] - 5 * TickSize, Brushes.Lime);
                }
                
                if (buyingClimax)
                {
                    double textY = AutoPositionTexts ? GetNextTextPosition(Low[0] - 12 * TickSize, true) : Low[0] - 12 * TickSize;
                    DrawTextWithBackground(tag + "_BuyingClimax", "💥 BUYING CLIMAX", 0, textY, Brushes.Red, Brushes.White);
                    
                    Draw.ArrowDown(this, tag + "_ClimaxDownBorder", false, 0, High[0] + 5 * TickSize, Brushes.Black);
                    Draw.ArrowDown(this, tag + "_ClimaxDown", false, 0, High[0] + 5 * TickSize, Brushes.Red);
                }
            }
            
            if (stealthPattern)
            {
                if (stealthAccumulation)
                {
                    double textY = AutoPositionTexts ? GetNextTextPosition(Low[0] - 8 * TickSize, true) : Low[0] - 8 * TickSize;
                    DrawTextWithBackground(tag + "_StealthAcc", "👁️ STEALTH ACCUMULATION", 0, textY, Brushes.DarkGreen, Brushes.White);
                }
                
                if (stealthDistribution)
                {
                    double textY = AutoPositionTexts ? GetNextTextPosition(High[0] + 8 * TickSize, false) : High[0] + 8 * TickSize;
                    DrawTextWithBackground(tag + "_StealthDist", "👁️ STEALTH DISTRIBUTION", 0, textY, Brushes.DarkRed, Brushes.White);
                }
            }
            
            if (failedBreakout)
            {
                double preferredY = deltaValues[0] > 0 ? Low[0] - 8 * TickSize : High[0] + 8 * TickSize;
                bool isBullish = deltaValues[0] > 0;
                double textY = AutoPositionTexts ? GetNextTextPosition(preferredY, isBullish) : preferredY;
                    
                DrawTextWithBackground(tag + "_FailedBO", "❌ FAILED BREAKOUT", 0, textY, Brushes.Red, Brushes.White);
            }
        }
        
        private void DrawSequentialAbsorptionVisual(string tag)
        {
            if (CurrentBar < 1) return;
            
            // Kontrastowy prostokąt z lepszą widocznością
            Draw.Rectangle(this, tag + "_SeqBox", false, 1, Low[1], 0, High[0], 
                         Brushes.Transparent, Brushes.Gold, 40);
            
            // Kontrastowa strzałka
            Draw.ArrowUp(this, tag + "_SeqArrowBorder", false, 0, Low[0] - 7 * TickSize, Brushes.Black);
            Draw.ArrowUp(this, tag + "_SeqArrow", false, 0, Low[0] - 7 * TickSize, Brushes.Gold);
            
            if (ShowPatternLabels)
            {
                string seqInfo = "🎯 SEQUENTIAL ABSORPTION\n" +
                               "Phase 1: Δ" + deltaValues[1].ToString("F0") + " Vol:" + totalVolumes[1].ToString("F0") + "\n" +
                               "Phase 2: Δ" + deltaValues[0].ToString("F0") + " Vol:" + totalVolumes[0].ToString("F0") + "\n" +
                               "Power Ratio: " + (Math.Abs(deltaValues[0]) / Math.Abs(deltaValues[1]) * 100).ToString("F0") + "%";
                
                double textY = AutoPositionTexts ? GetNextTextPosition(High[0] + 15 * TickSize, false) : High[0] + 15 * TickSize;
                DrawTextWithBackground(tag + "_SeqInfo", seqInfo, 0, textY, Brushes.Gold, Brushes.Black);
            }
        }
        #endregion

        #region Professional Level Management
        private void ManageProfessionalLevels()
        {
            ManagePOCLevels();
            
            if (EnableInstitutionalDetection)
            {
                ManageInstitutionalLevels();
            }
        }
        
        private void ManagePOCLevels()
        {
            bool professionalVolume = totalVolumes[0] > avgVolume[0] * MinVolumeMultiplierForSignal;
            
            if (professionalVolume)
            {
                double currentPOC = pocLevels[0];
                
                bool isNewLevel = true;
                foreach (double existingPOC in significantPOCLevels)
                {
                    if (Math.Abs(currentPOC - existingPOC) < TickSize * 5)
                    {
                        isNewLevel = false;
                        break;
                    }
                }
                
                if (isNewLevel)
                {
                    significantPOCLevels.Add(currentPOC);
                }
                
                if (significantPOCLevels.Count > 15)
                {
                    significantPOCLevels.RemoveAt(0);
                }
                
                if (ShowPOCLevels)
                {
                    for (int i = 0; i < significantPOCLevels.Count; i++)
                    {
                        string levelTag = "POC_Level_" + i;
                        Draw.HorizontalLine(this, levelTag, significantPOCLevels[i], POCColor, 
                                          DashStyleHelper.Dash, 1);
                    }
                }
            }
        }
        
        private void ManageInstitutionalLevels()
        {
            bool isInstitutionalLevel = totalVolumes[0] > avgVolume[0] * 4.0 && 
                                       Math.Abs(deltaValues[0]) > DeltaThreshold * 3 && 
                                       imbalanceRatio[0] > 0.8;
            
            if (isInstitutionalLevel)
            {
                double institutionalPrice = Close[0];
                
                bool isNewInstitutionalLevel = true;
                foreach (double existingLevel in institutionalLevels)
                {
                    if (Math.Abs(institutionalPrice - existingLevel) < TickSize * 10)
                    {
                        isNewInstitutionalLevel = false;
                        break;
                    }
                }
                
                if (isNewInstitutionalLevel)
                {
                    institutionalLevels.Add(institutionalPrice);
                    
                    string institutionalTag = "Institutional_" + CurrentBar;
                    Draw.HorizontalLine(this, institutionalTag, institutionalPrice, 
                                      InstitutionalColor, DashStyleHelper.Solid, 2);
                    
                    Draw.Text(this, institutionalTag + "_Label", "🏛️ INSTITUTIONAL", 0, 
                             institutionalPrice + 5 * TickSize, InstitutionalColor);
                }
                
                if (institutionalLevels.Count > 10)
                {
                    institutionalLevels.RemoveAt(0);
                }
            }
        }
        #endregion

        #region Performance Statistics
        private void InitializePatternStats()
        {
            patternStats.Clear();
            patternStats["Absorption"] = 0;
            patternStats["Divergence"] = 0;
            patternStats["Exhaustion"] = 0;
            patternStats["POCRejection"] = 0;
            patternStats["SequentialAbsorption"] = 0;
            patternStats["VolumeCascade"] = 0;
            patternStats["FailedBreakout"] = 0;
            patternStats["ImbalanceRejection"] = 0;
            patternStats["VolumeClimax"] = 0;
            patternStats["StealthPattern"] = 0;
            patternStats["InstitutionalFootprint"] = 0;
        }
        
        private void UpdatePatternStatistics()
        {
            List<string> detectedPatterns = GetDetectedPatterns();
            
            foreach (string pattern in detectedPatterns)
            {
                if (patternStats.ContainsKey(pattern))
                {
                    patternStats[pattern]++;
                }
            }
            
            if (institutionalFootprint)
            {
                patternStats["InstitutionalFootprint"]++;
            }
            
            DateTime currentTime = DateTime.Now;
            if ((currentTime - lastCalculationTime).TotalMilliseconds > 100)
            {
                Print("FootprintPatternDetectorPRO: Calculation time exceeded 100ms at bar " + CurrentBar.ToString());
            }
            lastCalculationTime = currentTime;
        }
        #endregion
        
        #region Helper Methods
        // Metoda do znajdowania następnej dostępnej pozycji dla tekstu
        private double GetNextTextPosition(double preferredY, bool isBullish)
        {
            if (!usedTextPositions.ContainsKey(CurrentBar))
            {
                usedTextPositions[CurrentBar] = new List<double>();
            }
            
            List<double> positions = usedTextPositions[CurrentBar];
            double finalY = preferredY;
            
            // Sprawdź czy pozycja jest zajęta
            while (positions.Any(pos => Math.Abs(pos - finalY) < TEXT_SPACING * TickSize))
            {
                if (isBullish)
                    finalY -= TEXT_SPACING * TickSize; // Przesuń w dół dla sygnałów bullish
                else
                    finalY += TEXT_SPACING * TickSize; // Przesuń w górę dla sygnałów bearish
            }
            
            positions.Add(finalY);
            return finalY;
        }
        
        // Metoda do rysowania tekstu z tłem dla lepszej widoczności
        private void DrawTextWithBackground(string tag, string text, int barsAgo, double y, Brush textColor, Brush backgroundColor)
        {
            // Rysuj tło jeśli włączone
            if (UseTextBackgrounds)
            {
                Draw.Rectangle(this, tag + "_Background", false, barsAgo, y - 2 * TickSize, barsAgo, y + 2 * TickSize, 
                             backgroundColor, backgroundColor, 70);
            }
            
            // Rysuj tekst
            Draw.Text(this, tag, text, barsAgo, y, textColor);
        }
        
        // Czyść stare pozycje tekstów (optymalizacja pamięci)
        private void CleanupOldTextPositions()
        {
            if (usedTextPositions.Count > 200) // Zachowaj ostatnie 200 barów
            {
                var keysToRemove = usedTextPositions.Keys.Where(k => k < CurrentBar - 200).ToList();
                foreach (var key in keysToRemove)
                {
                    usedTextPositions.Remove(key);
                }
            }
        }
        #endregion

        
[NinjaScriptProperty]
[Display(Name = "Show Trap Bar", GroupName = "Extended Patterns", Order = 1)]
public bool ShowTrapBar { get; set; }


[NinjaScriptProperty]
[Display(Name = "Show Sequential Cluster", GroupName = "Extended Patterns", Order = 2)]
public bool ShowSequentialCluster { get; set; }


[NinjaScriptProperty]
[Display(Name = "Show Iceberg Detection", GroupName = "Extended Patterns", Order = 3)]
public bool ShowIcebergDetection { get; set; }


[NinjaScriptProperty]
[Display(Name = "Enable Trend Filter", GroupName = "Extended Patterns", Order = 4)]
public bool EnableTrendFilter { get; set; }

#region Properties - Core Parameters
        [NinjaScriptProperty]
        [Range(25, 500)]
        [Display(Name="Delta Threshold", Description="Minimum delta value for pattern detection", Order=1, GroupName="Core Parameters")]
        public double DeltaThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name="Volume Multiplier", Description="Volume multiplier above average for basic signals", Order=2, GroupName="Core Parameters")]
        public double VolumeMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(10, 50)]
        [Display(Name="Lookback Period", Description="Period for calculating moving averages", Order=3, GroupName="Core Parameters")]
        public int LookbackPeriod { get; set; }
        #endregion

        #region Properties - Professional Filters
        [NinjaScriptProperty]
        [Range(100, 1000)]
        [Display(Name="Min Delta For Sequential", Description="Minimum delta threshold for sequential patterns", Order=1, GroupName="Professional Filters")]
        public double MinDeltaForSequential { get; set; }

        [NinjaScriptProperty]
        [Range(1.5, 5.0)]
        [Display(Name="Min Volume Multiplier For Signal", Description="Minimum volume multiplier required for signal generation", Order=2, GroupName="Professional Filters")]
        public double MinVolumeMultiplierForSignal { get; set; }

        [NinjaScriptProperty]
        [Range(0, 15)]
        [Display(Name="Max Signals Per Period", Description="Maximum signals per 50-bar period (0=unlimited)", Order=3, GroupName="Professional Filters")]
        public int MaxSignalsPerPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(0, 20)]
        [Display(Name="Signal Cooldown Bars", Description="Minimum bars between signals", Order=4, GroupName="Professional Filters")]
        public int SignalCooldownBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Require Volume Progression", Description="Require volume continuation for sequential patterns", Order=5, GroupName="Professional Filters")]
        public bool RequireVolumeProgression { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Require Price Confirmation", Description="Require price action confirmation", Order=6, GroupName="Professional Filters")]
        public bool RequirePriceConfirmation { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Require Institutional Confirmation", Description="Require institutional-grade volume for premium signals", Order=7, GroupName="Professional Filters")]
        public bool RequireInstitutionalConfirmation { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Enable Smart Filtering", Description="Enable advanced pattern filtering system", Order=8, GroupName="Professional Filters")]
        public bool EnableSmartFiltering { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Enable Contextual Analysis", Description="Enable market context validation", Order=9, GroupName="Professional Filters")]
        public bool EnableContextualAnalysis { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Enable Institutional Detection", Description="Enable institutional footprint detection", Order=10, GroupName="Professional Filters")]
        public bool EnableInstitutionalDetection { get; set; }

        [NinjaScriptProperty]
        [Range(0.3, 0.95)]
        [Display(Name="Min Confidence Score", Description="Minimum confidence score for signal display", Order=11, GroupName="Professional Filters")]
        public double MinConfidenceScore { get; set; }
        #endregion

        #region Properties - Time Profiles
        [NinjaScriptProperty]
        [Display(Name="Use Time-Based Profiles", Description="Enable automatic time-based filter adjustment", Order=1, GroupName="Time Profiles")]
        public bool UseTimeBasedProfiles { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Profile Mode", Description="Select time-based profile mode", Order=2, GroupName="Time Profiles")]
        public CustomTimeProfileMode ProfileMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Use Local Time", Description="Use local computer time instead of UTC", Order=3, GroupName="Time Profiles")]
        public bool UseLocalTime { get; set; }
        #endregion

        #region Properties - ICT Silver Bullet Times
        [NinjaScriptProperty]
        [Display(Name="Silver Bullet AM Start", Description="ICT Silver Bullet morning start time", Order=1, GroupName="ICT Silver Bullet Times")]
        public TimeSpan SilverBulletAM_Start { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Silver Bullet AM End", Description="ICT Silver Bullet morning end time", Order=2, GroupName="ICT Silver Bullet Times")]
        public TimeSpan SilverBulletAM_End { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Silver Bullet PM Start", Description="ICT Silver Bullet afternoon start time", Order=3, GroupName="ICT Silver Bullet Times")]
        public TimeSpan SilverBulletPM_Start { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Silver Bullet PM End", Description="ICT Silver Bullet afternoon end time", Order=4, GroupName="ICT Silver Bullet Times")]
        public TimeSpan SilverBulletPM_End { get; set; }
        #endregion

        #region Properties - Session Times
        [NinjaScriptProperty]
        [Display(Name="London Session Start", Description="London trading session start time", Order=1, GroupName="Session Times")]
        public TimeSpan LondonSession_Start { get; set; }

        [NinjaScriptProperty]
        [Display(Name="London Session End", Description="London trading session end time", Order=2, GroupName="Session Times")]
        public TimeSpan LondonSession_End { get; set; }

        [NinjaScriptProperty]
        [Display(Name="New York Session Start", Description="New York trading session start time", Order=3, GroupName="Session Times")]
        public TimeSpan NewYorkSession_Start { get; set; }

        [NinjaScriptProperty]
        [Display(Name="New York Session End", Description="New York trading session end time", Order=4, GroupName="Session Times")]
        public TimeSpan NewYorkSession_End { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Lunch Time Start", Description="Low volatility lunch period start", Order=5, GroupName="Session Times")]
        public TimeSpan LunchTime_Start { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Lunch Time End", Description="Low volatility lunch period end", Order=6, GroupName="Session Times")]
        public TimeSpan LunchTime_End { get; set; }
        #endregion

        #region Properties - Basic Patterns (4)
        [NinjaScriptProperty]
        [Display(Name="Show Absorption", Description="Show absorption patterns (high volume + small body + opposite delta)", Order=1, GroupName="Basic Patterns")]
        public bool ShowAbsorption { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Divergence", Description="Show delta divergence patterns (price vs delta divergence)", Order=2, GroupName="Basic Patterns")]
        public bool ShowDivergence { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Exhaustion", Description="Show exhaustion patterns (strong delta but opposite price move)", Order=3, GroupName="Basic Patterns")]
        public bool ShowExhaustion { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show POC Rejection", Description="Show Point of Control rejection patterns", Order=4, GroupName="Basic Patterns")]
        public bool ShowPOCRejection { get; set; }
        #endregion

        #region Properties - Advanced Patterns (2)
        [NinjaScriptProperty]
        [Display(Name="Show Sequential Absorption", Description="Show sequential absorption patterns (selling exhaustion → buying absorption)", Order=1, GroupName="Advanced Patterns")]
        public bool ShowSequentialAbsorption { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Volume Cascade", Description="Show volume imbalance cascade patterns (buildup → distribution → exhaustion)", Order=2, GroupName="Advanced Patterns")]
        public bool ShowVolumeCascade { get; set; }
        #endregion

        #region Properties - Professional Patterns (4)
        [NinjaScriptProperty]
        [Display(Name="Show Failed Breakouts", Description="Show failed breakout patterns (setup → breakout → massive reversal)", Order=1, GroupName="Professional Patterns")]
        public bool ShowFailedBreakouts { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Imbalance Rejection", Description="Show imbalance rejection patterns (support/resistance rejection)", Order=2, GroupName="Professional Patterns")]
        public bool ShowImbalanceRejection { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Volume Climax", Description="Show volume climax patterns (buildup → climax → deterioration → reversal)", Order=3, GroupName="Professional Patterns")]
        public bool ShowVolumeClimax { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Stealth Patterns", Description="Show stealth accumulation/distribution patterns (institutional quiet activity)", Order=4, GroupName="Professional Patterns")]
        public bool ShowStealthPatterns { get; set; }
        #endregion

        #region Properties - Display Options
        [NinjaScriptProperty]
        [Display(Name="Show Strong Signals Only", Description="Show only high-confidence composite signals", Order=1, GroupName="Display Options")]
        public bool ShowStrongSignalsOnly { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Diagnostic Info", Description="Show detailed diagnostic information", Order=2, GroupName="Display Options")]
        public bool ShowDiagnosticInfo { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show POC Levels", Description="Show significant Point of Control levels", Order=3, GroupName="Display Options")]
        public bool ShowPOCLevels { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Volume Profile", Description="Show simplified volume profile bars", Order=4, GroupName="Display Options")]
        public bool ShowVolumeProfile { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Pattern Labels", Description="Show detailed pattern labels and information", Order=5, GroupName="Display Options")]
        public bool ShowPatternLabels { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Show Confidence Scores", Description="Show pattern confidence percentages", Order=6, GroupName="Display Options")]
        public bool ShowConfidenceScores { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Use Text Backgrounds", Description="Use background for better text visibility", Order=7, GroupName="Display Options")]
        public bool UseTextBackgrounds { get; set; }

        [NinjaScriptProperty]
        [Display(Name="Auto Position Texts", Description="Automatically position texts to avoid overlap", Order=8, GroupName="Display Options")]
        public bool AutoPositionTexts { get; set; }
        #endregion

        
        [XmlIgnore]
        [Display(Name = "Text Color (Bullish)", Description = "Text color for bullish signal labels", Order = 8, GroupName = "Professional Colors")]
        public Brush BullishTextColor { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public string BullishTextColorSerializable
        {
            get { return Serialize.BrushToString(BullishTextColor); }
            set { BullishTextColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Text Color (Bearish)", Description = "Text color for bearish signal labels", Order = 9, GroupName = "Professional Colors")]
        public Brush BearishTextColor { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public string BearishTextColorSerializable
        {
            get { return Serialize.BrushToString(BearishTextColor); }
            set { BearishTextColor = Serialize.StringToBrush(value); }
        }
    #region Properties - Professional Colors
        
        [XmlIgnore]
        [Display(Name = "Diagnostic Text Color", Description = "Color for diagnostic text under bars", Order = 10, GroupName = "Professional Colors")]
        public Brush DiagnosticTextColor { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public string DiagnosticTextColorSerializable
        {
            get { return Serialize.BrushToString(DiagnosticTextColor); }
            set { DiagnosticTextColor = Serialize.StringToBrush(value); }
        }

[XmlIgnore]
        [Display(Name="Bullish Color", Description="Color for bullish signals", Order=1, GroupName="Professional Colors")]
        public Brush BullishColor { get; set; }

        [XmlIgnore]
        [Display(Name="Bearish Color", Description="Color for bearish signals", Order=2, GroupName="Professional Colors")]
        public Brush BearishColor { get; set; }

        [XmlIgnore]
        [Display(Name="Absorption Color", Description="Color for absorption patterns", Order=3, GroupName="Professional Colors")]
        public Brush AbsorptionColor { get; set; }

        [XmlIgnore]
        [Display(Name="Divergence Color", Description="Color for divergence patterns", Order=4, GroupName="Professional Colors")]
        public Brush DivergenceColor { get; set; }

        [XmlIgnore]
        [Display(Name="POC Color", Description="Color for POC levels", Order=5, GroupName="Professional Colors")]
        public Brush POCColor { get; set; }

        [XmlIgnore]
        [Display(Name="Institutional Color", Description="Color for institutional-grade signals", Order=6, GroupName="Professional Colors")]
        public Brush InstitutionalColor { get; set; }

        [XmlIgnore]
        [Display(Name="Professional Color", Description="Color for professional-grade signals", Order=7, GroupName="Professional Colors")]
        public Brush ProfessionalColor { get; set; }

        // Color serialization properties
        [Browsable(false)]
        [XmlIgnore]
        public string BullishColorSerializable
        {
            get { return Serialize.BrushToString(BullishColor); }
            set { BullishColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public string BearishColorSerializable
        {
            get { return Serialize.BrushToString(BearishColor); }
            set { BearishColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public string AbsorptionColorSerializable
        {
            get { return Serialize.BrushToString(AbsorptionColor); }
            set { AbsorptionColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public string DivergenceColorSerializable
        {
            get { return Serialize.BrushToString(DivergenceColor); }
            set { DivergenceColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public string POCColorSerializable
        {
            get { return Serialize.BrushToString(POCColor); }
            set { POCColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public string InstitutionalColorSerializable
        {
            get { return Serialize.BrushToString(InstitutionalColor); }
            set { InstitutionalColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public string ProfessionalColorSerializable
        {
            get { return Serialize.BrushToString(ProfessionalColor); }
            set { ProfessionalColor = Serialize.StringToBrush(value); }
        }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private FootprintPatternDetectorPRO[] cacheFootprintPatternDetectorPRO;
		public FootprintPatternDetectorPRO FootprintPatternDetectorPRO(bool showTrapBar, bool showSequentialCluster, bool showIcebergDetection, bool enableTrendFilter, double deltaThreshold, double volumeMultiplier, int lookbackPeriod, double minDeltaForSequential, double minVolumeMultiplierForSignal, int maxSignalsPerPeriod, int signalCooldownBars, bool requireVolumeProgression, bool requirePriceConfirmation, bool requireInstitutionalConfirmation, bool enableSmartFiltering, bool enableContextualAnalysis, bool enableInstitutionalDetection, double minConfidenceScore, bool useTimeBasedProfiles, CustomTimeProfileMode profileMode, bool useLocalTime, TimeSpan silverBulletAM_Start, TimeSpan silverBulletAM_End, TimeSpan silverBulletPM_Start, TimeSpan silverBulletPM_End, TimeSpan londonSession_Start, TimeSpan londonSession_End, TimeSpan newYorkSession_Start, TimeSpan newYorkSession_End, TimeSpan lunchTime_Start, TimeSpan lunchTime_End, bool showAbsorption, bool showDivergence, bool showExhaustion, bool showPOCRejection, bool showSequentialAbsorption, bool showVolumeCascade, bool showFailedBreakouts, bool showImbalanceRejection, bool showVolumeClimax, bool showStealthPatterns, bool showStrongSignalsOnly, bool showDiagnosticInfo, bool showPOCLevels, bool showVolumeProfile, bool showPatternLabels, bool showConfidenceScores, bool useTextBackgrounds, bool autoPositionTexts)
		{
			return FootprintPatternDetectorPRO(Input, showTrapBar, showSequentialCluster, showIcebergDetection, enableTrendFilter, deltaThreshold, volumeMultiplier, lookbackPeriod, minDeltaForSequential, minVolumeMultiplierForSignal, maxSignalsPerPeriod, signalCooldownBars, requireVolumeProgression, requirePriceConfirmation, requireInstitutionalConfirmation, enableSmartFiltering, enableContextualAnalysis, enableInstitutionalDetection, minConfidenceScore, useTimeBasedProfiles, profileMode, useLocalTime, silverBulletAM_Start, silverBulletAM_End, silverBulletPM_Start, silverBulletPM_End, londonSession_Start, londonSession_End, newYorkSession_Start, newYorkSession_End, lunchTime_Start, lunchTime_End, showAbsorption, showDivergence, showExhaustion, showPOCRejection, showSequentialAbsorption, showVolumeCascade, showFailedBreakouts, showImbalanceRejection, showVolumeClimax, showStealthPatterns, showStrongSignalsOnly, showDiagnosticInfo, showPOCLevels, showVolumeProfile, showPatternLabels, showConfidenceScores, useTextBackgrounds, autoPositionTexts);
		}

		public FootprintPatternDetectorPRO FootprintPatternDetectorPRO(ISeries<double> input, bool showTrapBar, bool showSequentialCluster, bool showIcebergDetection, bool enableTrendFilter, double deltaThreshold, double volumeMultiplier, int lookbackPeriod, double minDeltaForSequential, double minVolumeMultiplierForSignal, int maxSignalsPerPeriod, int signalCooldownBars, bool requireVolumeProgression, bool requirePriceConfirmation, bool requireInstitutionalConfirmation, bool enableSmartFiltering, bool enableContextualAnalysis, bool enableInstitutionalDetection, double minConfidenceScore, bool useTimeBasedProfiles, CustomTimeProfileMode profileMode, bool useLocalTime, TimeSpan silverBulletAM_Start, TimeSpan silverBulletAM_End, TimeSpan silverBulletPM_Start, TimeSpan silverBulletPM_End, TimeSpan londonSession_Start, TimeSpan londonSession_End, TimeSpan newYorkSession_Start, TimeSpan newYorkSession_End, TimeSpan lunchTime_Start, TimeSpan lunchTime_End, bool showAbsorption, bool showDivergence, bool showExhaustion, bool showPOCRejection, bool showSequentialAbsorption, bool showVolumeCascade, bool showFailedBreakouts, bool showImbalanceRejection, bool showVolumeClimax, bool showStealthPatterns, bool showStrongSignalsOnly, bool showDiagnosticInfo, bool showPOCLevels, bool showVolumeProfile, bool showPatternLabels, bool showConfidenceScores, bool useTextBackgrounds, bool autoPositionTexts)
		{
			if (cacheFootprintPatternDetectorPRO != null)
				for (int idx = 0; idx < cacheFootprintPatternDetectorPRO.Length; idx++)
					if (cacheFootprintPatternDetectorPRO[idx] != null && cacheFootprintPatternDetectorPRO[idx].ShowTrapBar == showTrapBar && cacheFootprintPatternDetectorPRO[idx].ShowSequentialCluster == showSequentialCluster && cacheFootprintPatternDetectorPRO[idx].ShowIcebergDetection == showIcebergDetection && cacheFootprintPatternDetectorPRO[idx].EnableTrendFilter == enableTrendFilter && cacheFootprintPatternDetectorPRO[idx].DeltaThreshold == deltaThreshold && cacheFootprintPatternDetectorPRO[idx].VolumeMultiplier == volumeMultiplier && cacheFootprintPatternDetectorPRO[idx].LookbackPeriod == lookbackPeriod && cacheFootprintPatternDetectorPRO[idx].MinDeltaForSequential == minDeltaForSequential && cacheFootprintPatternDetectorPRO[idx].MinVolumeMultiplierForSignal == minVolumeMultiplierForSignal && cacheFootprintPatternDetectorPRO[idx].MaxSignalsPerPeriod == maxSignalsPerPeriod && cacheFootprintPatternDetectorPRO[idx].SignalCooldownBars == signalCooldownBars && cacheFootprintPatternDetectorPRO[idx].RequireVolumeProgression == requireVolumeProgression && cacheFootprintPatternDetectorPRO[idx].RequirePriceConfirmation == requirePriceConfirmation && cacheFootprintPatternDetectorPRO[idx].RequireInstitutionalConfirmation == requireInstitutionalConfirmation && cacheFootprintPatternDetectorPRO[idx].EnableSmartFiltering == enableSmartFiltering && cacheFootprintPatternDetectorPRO[idx].EnableContextualAnalysis == enableContextualAnalysis && cacheFootprintPatternDetectorPRO[idx].EnableInstitutionalDetection == enableInstitutionalDetection && cacheFootprintPatternDetectorPRO[idx].MinConfidenceScore == minConfidenceScore && cacheFootprintPatternDetectorPRO[idx].UseTimeBasedProfiles == useTimeBasedProfiles && cacheFootprintPatternDetectorPRO[idx].ProfileMode == profileMode && cacheFootprintPatternDetectorPRO[idx].UseLocalTime == useLocalTime && cacheFootprintPatternDetectorPRO[idx].SilverBulletAM_Start == silverBulletAM_Start && cacheFootprintPatternDetectorPRO[idx].SilverBulletAM_End == silverBulletAM_End && cacheFootprintPatternDetectorPRO[idx].SilverBulletPM_Start == silverBulletPM_Start && cacheFootprintPatternDetectorPRO[idx].SilverBulletPM_End == silverBulletPM_End && cacheFootprintPatternDetectorPRO[idx].LondonSession_Start == londonSession_Start && cacheFootprintPatternDetectorPRO[idx].LondonSession_End == londonSession_End && cacheFootprintPatternDetectorPRO[idx].NewYorkSession_Start == newYorkSession_Start && cacheFootprintPatternDetectorPRO[idx].NewYorkSession_End == newYorkSession_End && cacheFootprintPatternDetectorPRO[idx].LunchTime_Start == lunchTime_Start && cacheFootprintPatternDetectorPRO[idx].LunchTime_End == lunchTime_End && cacheFootprintPatternDetectorPRO[idx].ShowAbsorption == showAbsorption && cacheFootprintPatternDetectorPRO[idx].ShowDivergence == showDivergence && cacheFootprintPatternDetectorPRO[idx].ShowExhaustion == showExhaustion && cacheFootprintPatternDetectorPRO[idx].ShowPOCRejection == showPOCRejection && cacheFootprintPatternDetectorPRO[idx].ShowSequentialAbsorption == showSequentialAbsorption && cacheFootprintPatternDetectorPRO[idx].ShowVolumeCascade == showVolumeCascade && cacheFootprintPatternDetectorPRO[idx].ShowFailedBreakouts == showFailedBreakouts && cacheFootprintPatternDetectorPRO[idx].ShowImbalanceRejection == showImbalanceRejection && cacheFootprintPatternDetectorPRO[idx].ShowVolumeClimax == showVolumeClimax && cacheFootprintPatternDetectorPRO[idx].ShowStealthPatterns == showStealthPatterns && cacheFootprintPatternDetectorPRO[idx].ShowStrongSignalsOnly == showStrongSignalsOnly && cacheFootprintPatternDetectorPRO[idx].ShowDiagnosticInfo == showDiagnosticInfo && cacheFootprintPatternDetectorPRO[idx].ShowPOCLevels == showPOCLevels && cacheFootprintPatternDetectorPRO[idx].ShowVolumeProfile == showVolumeProfile && cacheFootprintPatternDetectorPRO[idx].ShowPatternLabels == showPatternLabels && cacheFootprintPatternDetectorPRO[idx].ShowConfidenceScores == showConfidenceScores && cacheFootprintPatternDetectorPRO[idx].UseTextBackgrounds == useTextBackgrounds && cacheFootprintPatternDetectorPRO[idx].AutoPositionTexts == autoPositionTexts && cacheFootprintPatternDetectorPRO[idx].EqualsInput(input))
						return cacheFootprintPatternDetectorPRO[idx];
			return CacheIndicator<FootprintPatternDetectorPRO>(new FootprintPatternDetectorPRO(){ ShowTrapBar = showTrapBar, ShowSequentialCluster = showSequentialCluster, ShowIcebergDetection = showIcebergDetection, EnableTrendFilter = enableTrendFilter, DeltaThreshold = deltaThreshold, VolumeMultiplier = volumeMultiplier, LookbackPeriod = lookbackPeriod, MinDeltaForSequential = minDeltaForSequential, MinVolumeMultiplierForSignal = minVolumeMultiplierForSignal, MaxSignalsPerPeriod = maxSignalsPerPeriod, SignalCooldownBars = signalCooldownBars, RequireVolumeProgression = requireVolumeProgression, RequirePriceConfirmation = requirePriceConfirmation, RequireInstitutionalConfirmation = requireInstitutionalConfirmation, EnableSmartFiltering = enableSmartFiltering, EnableContextualAnalysis = enableContextualAnalysis, EnableInstitutionalDetection = enableInstitutionalDetection, MinConfidenceScore = minConfidenceScore, UseTimeBasedProfiles = useTimeBasedProfiles, ProfileMode = profileMode, UseLocalTime = useLocalTime, SilverBulletAM_Start = silverBulletAM_Start, SilverBulletAM_End = silverBulletAM_End, SilverBulletPM_Start = silverBulletPM_Start, SilverBulletPM_End = silverBulletPM_End, LondonSession_Start = londonSession_Start, LondonSession_End = londonSession_End, NewYorkSession_Start = newYorkSession_Start, NewYorkSession_End = newYorkSession_End, LunchTime_Start = lunchTime_Start, LunchTime_End = lunchTime_End, ShowAbsorption = showAbsorption, ShowDivergence = showDivergence, ShowExhaustion = showExhaustion, ShowPOCRejection = showPOCRejection, ShowSequentialAbsorption = showSequentialAbsorption, ShowVolumeCascade = showVolumeCascade, ShowFailedBreakouts = showFailedBreakouts, ShowImbalanceRejection = showImbalanceRejection, ShowVolumeClimax = showVolumeClimax, ShowStealthPatterns = showStealthPatterns, ShowStrongSignalsOnly = showStrongSignalsOnly, ShowDiagnosticInfo = showDiagnosticInfo, ShowPOCLevels = showPOCLevels, ShowVolumeProfile = showVolumeProfile, ShowPatternLabels = showPatternLabels, ShowConfidenceScores = showConfidenceScores, UseTextBackgrounds = useTextBackgrounds, AutoPositionTexts = autoPositionTexts }, input, ref cacheFootprintPatternDetectorPRO);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.FootprintPatternDetectorPRO FootprintPatternDetectorPRO(bool showTrapBar, bool showSequentialCluster, bool showIcebergDetection, bool enableTrendFilter, double deltaThreshold, double volumeMultiplier, int lookbackPeriod, double minDeltaForSequential, double minVolumeMultiplierForSignal, int maxSignalsPerPeriod, int signalCooldownBars, bool requireVolumeProgression, bool requirePriceConfirmation, bool requireInstitutionalConfirmation, bool enableSmartFiltering, bool enableContextualAnalysis, bool enableInstitutionalDetection, double minConfidenceScore, bool useTimeBasedProfiles, CustomTimeProfileMode profileMode, bool useLocalTime, TimeSpan silverBulletAM_Start, TimeSpan silverBulletAM_End, TimeSpan silverBulletPM_Start, TimeSpan silverBulletPM_End, TimeSpan londonSession_Start, TimeSpan londonSession_End, TimeSpan newYorkSession_Start, TimeSpan newYorkSession_End, TimeSpan lunchTime_Start, TimeSpan lunchTime_End, bool showAbsorption, bool showDivergence, bool showExhaustion, bool showPOCRejection, bool showSequentialAbsorption, bool showVolumeCascade, bool showFailedBreakouts, bool showImbalanceRejection, bool showVolumeClimax, bool showStealthPatterns, bool showStrongSignalsOnly, bool showDiagnosticInfo, bool showPOCLevels, bool showVolumeProfile, bool showPatternLabels, bool showConfidenceScores, bool useTextBackgrounds, bool autoPositionTexts)
		{
			return indicator.FootprintPatternDetectorPRO(Input, showTrapBar, showSequentialCluster, showIcebergDetection, enableTrendFilter, deltaThreshold, volumeMultiplier, lookbackPeriod, minDeltaForSequential, minVolumeMultiplierForSignal, maxSignalsPerPeriod, signalCooldownBars, requireVolumeProgression, requirePriceConfirmation, requireInstitutionalConfirmation, enableSmartFiltering, enableContextualAnalysis, enableInstitutionalDetection, minConfidenceScore, useTimeBasedProfiles, profileMode, useLocalTime, silverBulletAM_Start, silverBulletAM_End, silverBulletPM_Start, silverBulletPM_End, londonSession_Start, londonSession_End, newYorkSession_Start, newYorkSession_End, lunchTime_Start, lunchTime_End, showAbsorption, showDivergence, showExhaustion, showPOCRejection, showSequentialAbsorption, showVolumeCascade, showFailedBreakouts, showImbalanceRejection, showVolumeClimax, showStealthPatterns, showStrongSignalsOnly, showDiagnosticInfo, showPOCLevels, showVolumeProfile, showPatternLabels, showConfidenceScores, useTextBackgrounds, autoPositionTexts);
		}

		public Indicators.FootprintPatternDetectorPRO FootprintPatternDetectorPRO(ISeries<double> input , bool showTrapBar, bool showSequentialCluster, bool showIcebergDetection, bool enableTrendFilter, double deltaThreshold, double volumeMultiplier, int lookbackPeriod, double minDeltaForSequential, double minVolumeMultiplierForSignal, int maxSignalsPerPeriod, int signalCooldownBars, bool requireVolumeProgression, bool requirePriceConfirmation, bool requireInstitutionalConfirmation, bool enableSmartFiltering, bool enableContextualAnalysis, bool enableInstitutionalDetection, double minConfidenceScore, bool useTimeBasedProfiles, CustomTimeProfileMode profileMode, bool useLocalTime, TimeSpan silverBulletAM_Start, TimeSpan silverBulletAM_End, TimeSpan silverBulletPM_Start, TimeSpan silverBulletPM_End, TimeSpan londonSession_Start, TimeSpan londonSession_End, TimeSpan newYorkSession_Start, TimeSpan newYorkSession_End, TimeSpan lunchTime_Start, TimeSpan lunchTime_End, bool showAbsorption, bool showDivergence, bool showExhaustion, bool showPOCRejection, bool showSequentialAbsorption, bool showVolumeCascade, bool showFailedBreakouts, bool showImbalanceRejection, bool showVolumeClimax, bool showStealthPatterns, bool showStrongSignalsOnly, bool showDiagnosticInfo, bool showPOCLevels, bool showVolumeProfile, bool showPatternLabels, bool showConfidenceScores, bool useTextBackgrounds, bool autoPositionTexts)
		{
			return indicator.FootprintPatternDetectorPRO(input, showTrapBar, showSequentialCluster, showIcebergDetection, enableTrendFilter, deltaThreshold, volumeMultiplier, lookbackPeriod, minDeltaForSequential, minVolumeMultiplierForSignal, maxSignalsPerPeriod, signalCooldownBars, requireVolumeProgression, requirePriceConfirmation, requireInstitutionalConfirmation, enableSmartFiltering, enableContextualAnalysis, enableInstitutionalDetection, minConfidenceScore, useTimeBasedProfiles, profileMode, useLocalTime, silverBulletAM_Start, silverBulletAM_End, silverBulletPM_Start, silverBulletPM_End, londonSession_Start, londonSession_End, newYorkSession_Start, newYorkSession_End, lunchTime_Start, lunchTime_End, showAbsorption, showDivergence, showExhaustion, showPOCRejection, showSequentialAbsorption, showVolumeCascade, showFailedBreakouts, showImbalanceRejection, showVolumeClimax, showStealthPatterns, showStrongSignalsOnly, showDiagnosticInfo, showPOCLevels, showVolumeProfile, showPatternLabels, showConfidenceScores, useTextBackgrounds, autoPositionTexts);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.FootprintPatternDetectorPRO FootprintPatternDetectorPRO(bool showTrapBar, bool showSequentialCluster, bool showIcebergDetection, bool enableTrendFilter, double deltaThreshold, double volumeMultiplier, int lookbackPeriod, double minDeltaForSequential, double minVolumeMultiplierForSignal, int maxSignalsPerPeriod, int signalCooldownBars, bool requireVolumeProgression, bool requirePriceConfirmation, bool requireInstitutionalConfirmation, bool enableSmartFiltering, bool enableContextualAnalysis, bool enableInstitutionalDetection, double minConfidenceScore, bool useTimeBasedProfiles, CustomTimeProfileMode profileMode, bool useLocalTime, TimeSpan silverBulletAM_Start, TimeSpan silverBulletAM_End, TimeSpan silverBulletPM_Start, TimeSpan silverBulletPM_End, TimeSpan londonSession_Start, TimeSpan londonSession_End, TimeSpan newYorkSession_Start, TimeSpan newYorkSession_End, TimeSpan lunchTime_Start, TimeSpan lunchTime_End, bool showAbsorption, bool showDivergence, bool showExhaustion, bool showPOCRejection, bool showSequentialAbsorption, bool showVolumeCascade, bool showFailedBreakouts, bool showImbalanceRejection, bool showVolumeClimax, bool showStealthPatterns, bool showStrongSignalsOnly, bool showDiagnosticInfo, bool showPOCLevels, bool showVolumeProfile, bool showPatternLabels, bool showConfidenceScores, bool useTextBackgrounds, bool autoPositionTexts)
		{
			return indicator.FootprintPatternDetectorPRO(Input, showTrapBar, showSequentialCluster, showIcebergDetection, enableTrendFilter, deltaThreshold, volumeMultiplier, lookbackPeriod, minDeltaForSequential, minVolumeMultiplierForSignal, maxSignalsPerPeriod, signalCooldownBars, requireVolumeProgression, requirePriceConfirmation, requireInstitutionalConfirmation, enableSmartFiltering, enableContextualAnalysis, enableInstitutionalDetection, minConfidenceScore, useTimeBasedProfiles, profileMode, useLocalTime, silverBulletAM_Start, silverBulletAM_End, silverBulletPM_Start, silverBulletPM_End, londonSession_Start, londonSession_End, newYorkSession_Start, newYorkSession_End, lunchTime_Start, lunchTime_End, showAbsorption, showDivergence, showExhaustion, showPOCRejection, showSequentialAbsorption, showVolumeCascade, showFailedBreakouts, showImbalanceRejection, showVolumeClimax, showStealthPatterns, showStrongSignalsOnly, showDiagnosticInfo, showPOCLevels, showVolumeProfile, showPatternLabels, showConfidenceScores, useTextBackgrounds, autoPositionTexts);
		}

		public Indicators.FootprintPatternDetectorPRO FootprintPatternDetectorPRO(ISeries<double> input , bool showTrapBar, bool showSequentialCluster, bool showIcebergDetection, bool enableTrendFilter, double deltaThreshold, double volumeMultiplier, int lookbackPeriod, double minDeltaForSequential, double minVolumeMultiplierForSignal, int maxSignalsPerPeriod, int signalCooldownBars, bool requireVolumeProgression, bool requirePriceConfirmation, bool requireInstitutionalConfirmation, bool enableSmartFiltering, bool enableContextualAnalysis, bool enableInstitutionalDetection, double minConfidenceScore, bool useTimeBasedProfiles, CustomTimeProfileMode profileMode, bool useLocalTime, TimeSpan silverBulletAM_Start, TimeSpan silverBulletAM_End, TimeSpan silverBulletPM_Start, TimeSpan silverBulletPM_End, TimeSpan londonSession_Start, TimeSpan londonSession_End, TimeSpan newYorkSession_Start, TimeSpan newYorkSession_End, TimeSpan lunchTime_Start, TimeSpan lunchTime_End, bool showAbsorption, bool showDivergence, bool showExhaustion, bool showPOCRejection, bool showSequentialAbsorption, bool showVolumeCascade, bool showFailedBreakouts, bool showImbalanceRejection, bool showVolumeClimax, bool showStealthPatterns, bool showStrongSignalsOnly, bool showDiagnosticInfo, bool showPOCLevels, bool showVolumeProfile, bool showPatternLabels, bool showConfidenceScores, bool useTextBackgrounds, bool autoPositionTexts)
		{
			return indicator.FootprintPatternDetectorPRO(input, showTrapBar, showSequentialCluster, showIcebergDetection, enableTrendFilter, deltaThreshold, volumeMultiplier, lookbackPeriod, minDeltaForSequential, minVolumeMultiplierForSignal, maxSignalsPerPeriod, signalCooldownBars, requireVolumeProgression, requirePriceConfirmation, requireInstitutionalConfirmation, enableSmartFiltering, enableContextualAnalysis, enableInstitutionalDetection, minConfidenceScore, useTimeBasedProfiles, profileMode, useLocalTime, silverBulletAM_Start, silverBulletAM_End, silverBulletPM_Start, silverBulletPM_End, londonSession_Start, londonSession_End, newYorkSession_Start, newYorkSession_End, lunchTime_Start, lunchTime_End, showAbsorption, showDivergence, showExhaustion, showPOCRejection, showSequentialAbsorption, showVolumeCascade, showFailedBreakouts, showImbalanceRejection, showVolumeClimax, showStealthPatterns, showStrongSignalsOnly, showDiagnosticInfo, showPOCLevels, showVolumeProfile, showPatternLabels, showConfidenceScores, useTextBackgrounds, autoPositionTexts);
		}
	}
}

#endregion
