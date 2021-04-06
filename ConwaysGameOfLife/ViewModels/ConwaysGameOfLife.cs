//   AptusLite - Conway's Game of Life Implementation
//   Copyright(C) 2021 - Brendan Price 
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see<https://www.gnu.org/licenses/>.
using ConwaysGameOfLife.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ConwaysGameOfLife.Sound;

namespace ConwaysGameOfLife.ViewModels
{
    public class Cell : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public double X { get; set; }
        public double Y { get; set; }

        private static readonly Random ran = new Random();

        private static readonly string[] colours = { "Pink", "Blue", "Yellow", "Purple", "Green", "Orange", "Red", "Black"};

        private string DefaultAliveColour { get; set; } = ConfigurationManager.AppSettings["DefaultAliveColour"];
        private string DefaultDeadColour { get; set; } = ConfigurationManager.AppSettings["DefaultDeadColour"];

        public string ColourStr
        {
            get
            {
                if(ViewModelCells.TechnoColour)
                {
                    return Alive ? GetRandomAliveColour() : DefaultDeadColour;
                }
                else
                {
                    return Alive ? DefaultAliveColour : DefaultDeadColour;
                }
            }
        }

        private string GetRandomAliveColour()
        {
            string randomAliveColour = colours[ran.Next(0, colours.Length)];
            while(randomAliveColour == DefaultDeadColour)
            {
                randomAliveColour = colours[ran.Next(0, colours.Length)];
            }
            return randomAliveColour;
        }

        private ICommand _toggleCmd = null;
        public ICommand ToggleCmd
        {
            get
            {
                if (_toggleCmd == null)
                {
                    _toggleCmd = new RelayCommand(
                        p => this.Toggle(),
                        p => this.CanToggle());
                }
                return _toggleCmd;
            }
        }

        public bool CanToggle()
        {
            return true;
        }

        public void Toggle()
        {
            Alive = !Alive;
            TempStateDuringNextStepCalculation = Alive;
            NotifyPropertyChanged("ColourStr");
        }

        private ICommand _makeAliveCmd = null;
        public ICommand MakeAliveCmd
        {
            get
            {
                if (_makeAliveCmd == null)
                {
                    _makeAliveCmd = new RelayCommand(
                        p => this.MakeAlive(),
                        p => this.CanMakeAlive());
                }
                return _makeAliveCmd;
            }
        }

        public bool CanMakeAlive()
        {
            if(!Alive)
            {
                return true;
            }
            return false;
        }

        public void MakeAlive()
        {
            Alive = true;
            NotifyPropertyChanged("ColourStr");
        }

        private bool _mAlive;
        public bool Alive 
        { 
            get
            {
                return _mAlive;
            }
            set
            {
                if(_mAlive != value)
                {
                    _mAlive = value;
                    NotifyPropertyChanged("ColourStr");
                }
                
            }
        }

        private bool _mTempStateDuringNextStepCalculation;
        public bool TempStateDuringNextStepCalculation
        {
            get
            {
                return _mTempStateDuringNextStepCalculation;
            }
            set
            {
                _mTempStateDuringNextStepCalculation = value;
            }
        }

        /// <summary>
        /// Determine if Cell cell is a neighbour of this cell.
        /// </summary>
        /// <param name="cell"></param>
        /// <returns>true if neighbour, false otherwise</returns>
        public bool IsNeighbour(Cell cell)
        {
            //If distance is < 2, it is a neighbour
            double val = Math.Sqrt(Math.Abs( Math.Pow(Convert.ToInt32(this.X / ViewModelCells.RatioOfCellToCanvas - cell.X / ViewModelCells.RatioOfCellToCanvas), 2) 
                + Math.Pow(Convert.ToInt32(this.Y / ViewModelCells.RatioOfCellToCanvas - cell.Y / ViewModelCells.RatioOfCellToCanvas), 2)));
            return val < 2 ? true : false;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public class ViewModelCells : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public enum STATE { START, STEP, PAUSE, STOP, CLEAR};
        public enum STATUS { EXTINCT, STABLE, RUNNING, MAX_GENERATION };

        private static int TimeBetweenGenerationsInMilliseconds = Convert.ToInt32(ConfigurationManager.AppSettings["TimeBetweenGenerationsInMilliseconds"]);
        private static int mCellByCell = 45; //Default, and maximum amount of X-by-Y cells.
        public static int RatioOfCellToCanvas = 10; 
        public int Width { get; set; } = mCellByCell * RatioOfCellToCanvas;
        public int Height { get; set; } = mCellByCell * RatioOfCellToCanvas;
        
        public IDictionary<Cell, List<Cell>> CellToNeighboursMapping { get; set; } = new Dictionary<Cell, List<Cell>>();//Faciliate list of neighbours per cell key
        public ObservableCollection<Cell> Cells { get; set; } = new ObservableCollection<Cell>();//WPF requirement

        public ViewModelCells()
        {
            CreateCells();
            State = STATE.STOP;
            Task.Run(GameLoop);
            Task.Run(PlayTheme);
        }

        private void GameLoop()
        {
            DateTime startFrameTime;
            while (true)
            {
                startFrameTime = DateTime.Now;              //Time at start of this frame
                switch (State)
                {
                    case STATE.START:
                        {
                            CalculateNextStep();
                            GridSizeSliderEnabled = false;
                            break;
                        }
                    case STATE.STEP:
                        {
                            CalculateNextStep();
                            State = STATE.PAUSE;
                            GridSizeSliderEnabled = false;
                            break;
                        }
                    case STATE.CLEAR:
                        {
                            App.Current.Dispatcher.Invoke(() => CreateCells());
                            State = STATE.STOP;
                            Status = STATUS.EXTINCT;
                            GridSizeSliderEnabled = true;
                            break;
                        }
                    case STATE.STOP:
                        {
                            GenerationNumber = 0;
                            PopulationCount = 0;
                            GridSizeSliderEnabled = true;
                            break;
                        }
                    case STATE.PAUSE:
                        {
                            GridSizeSliderEnabled = true;
                            break;
                        }
                }
                FrameSleep(startFrameTime); //Sleep between frames.
            }
        }

        private void CreateCells()
        {
            ClearGameOfLifeBoard();

            for (int y = 0; y < CellByCell; y++)
            {
                for (int x = 0; x < CellByCell; x++)
                {
                    Cells.Add(new Cell {X=x*RatioOfCellToCanvas,  Y=y*RatioOfCellToCanvas});
                }
            }

            Cells.ToList().ForEach(c => CalculateNeighbours(c));
        }

        private void ClearGameOfLifeBoard()
        {
            Cells.Clear();
            CellToNeighboursMapping.Clear();
            GenerationNumber = PopulationCount = 0;
        }

        public int CellByCell
        {
            get
            {
                return mCellByCell;
            }
            set
            {
                if(mCellByCell != value)
                {
                    mCellByCell = value;
                    CreateCells();
                    NotifyPropertyChanged("CellByCell");
                }
            }
        }

        private bool mGridSizeSliderEnabled = true;
        public bool GridSizeSliderEnabled
        {
            get
            {
                return mGridSizeSliderEnabled;
            }
            set
            {
                if(mGridSizeSliderEnabled != value)
                {
                    mGridSizeSliderEnabled = value;
                    NotifyPropertyChanged("GridSizeSliderEnabled");
                }
            }
        }

        /// <summary>
        /// Populate Dictionary of key 'cell', and value 'List<Cell>' which are the key cell's neighbours.
        /// </summary>
        private void CalculateNeighbours(Cell cell)
        {
            List<Cell> neighbours = new List<Cell>();
            neighbours.AddRange(Cells.Where(c => c != cell && cell.IsNeighbour(c)));
            CellToNeighboursMapping.Add(cell, neighbours);
        }

        /// <summary>
        /// Frame should last TimeBetweenGenerationsInMilliseconds in total.
        /// </summary>
        /// <param name="startFrameTime"></param>
        private void FrameSleep(DateTime startFrameTime)
        {
            int timeRunning = Convert.ToInt32(DateTime.Now.Subtract(startFrameTime).TotalMilliseconds);
            timeRunning = TimeBetweenGenerationsInMilliseconds - timeRunning < 0 ? 0 : timeRunning;
            Thread.Sleep(TimeBetweenGenerationsInMilliseconds - timeRunning); 
        }

        private ICommand _startCmd = null;
        public ICommand StartCmd
        {
            get
            {
                if (_startCmd == null)
                {
                    _startCmd = new RelayCommand(
                        p => Start(),
                        p => ReturnTrue());
                }
                return _startCmd;
            }
        }

        public void Start()
        {
            State = STATE.START;
        }

        private ICommand _stepCmd = null;
        public ICommand StepCmd
        {
            get
            {
                if (_stepCmd == null)
                {
                    _stepCmd = new RelayCommand(
                        p => Step(),
                        p => ReturnTrue());
                }
                return _stepCmd;
            }
        }

        public void Step()
        {
            State = STATE.STEP;
        }

        private ICommand _stopCmd = null;
        public ICommand StopCmd
        {
            get
            {
                if (_stopCmd == null)
                {
                    _stopCmd = new RelayCommand(
                        p => Stop(),
                        p => ReturnTrue());
                }
                return _stopCmd;
            }
        }

        public void Stop()
        {
            State = STATE.STOP;
        }

        private ICommand _pauseCmd = null;
        public ICommand PauseCmd
        {
            get
            {
                if (_pauseCmd == null)
                {
                    _pauseCmd = new RelayCommand(
                        p => Pause(),
                        p => ReturnTrue());
                }
                return _pauseCmd;
            }
        }

        public void Pause()
        {
            State = STATE.PAUSE;
        }

        private ICommand _clearCmd = null;
        public ICommand ClearCmd
        {
            get
            {
                if (_clearCmd == null)
                {
                    _clearCmd = new RelayCommand(
                        p => Clear(),
                        p => ReturnTrue());
                }
                return _clearCmd;
            }
        }

        public void Clear()
        {
            State = STATE.CLEAR;
        }

        private ICommand _exitCmd = null;
        public ICommand ExitCmd
        {
            get
            {
                if (_exitCmd == null)
                {
                    _exitCmd = new RelayCommand(
                        p => Exit(),
                        p => ReturnTrue());
                }
                return _exitCmd;
            }
        }

        private void Exit()
        {
            System.Windows.Application.Current.Shutdown();
        }

        private static volatile bool mTechnoColour = false;
        public static bool TechnoColour
        {
            get
            {
                return mTechnoColour;
            }
            set
            {
                if(mTechnoColour != value)
                {
                    mTechnoColour = value;
                }
            }
        }

        private STATE mState = STATE.STOP;
        public STATE State
        {
            get
            {
                return mState;
            }
            set
            {
                if(mState != value)
                {
                    mState = value;
                    NotifyPropertyChanged("State");
                }
            }
        }

        private STATUS mStatus = STATUS.EXTINCT;
        public STATUS Status
        {
            get
            {
                return mStatus;
            }
            set
            {
                if (mStatus != value)
                {
                    mStatus = value;
                    NotifyPropertyChanged("Status");
                }
            }
        }

        private int mPopulationCount;
        public int PopulationCount 
        {
            get
            {
                return mPopulationCount;
            }
            set
            {
                if(mPopulationCount != value)
                {
                    mPopulationCount = value;
                    NotifyPropertyChanged("PopulationCount");
                }
            }
        }

        private int mGenerationNumber;
        public int GenerationNumber
        {
            get
            {
                return mGenerationNumber;
            }
            set
            {
                if (mGenerationNumber != value)
                {
                    mGenerationNumber = value;
                    NotifyPropertyChanged("GenerationNumber");
                }
            }
        }

        private int mMaxGenerationNumber = Convert.ToInt32(ConfigurationManager.AppSettings["MaxGeneration"]);
        public int MaxGenerationNumber
        {
            get
            {
                return mMaxGenerationNumber;
            }
            set
            {
                if (mMaxGenerationNumber != value)
                {
                    mMaxGenerationNumber = value;
                    NotifyPropertyChanged("MaxGenerationNumber");
                }
            }
        }

        /// <summary>
        /// Determine which cells die, and which live.
        /// </summary>
        private void CalculateNextStep()
        {
            bool isStable = true;
            foreach (Cell cell in Cells.ToList().Where(c => c.Alive && (HowManyNeighboursAreAlive(c) < 2 || HowManyNeighboursAreAlive(c) > 3)))
            {
                    cell.TempStateDuringNextStepCalculation = false;
                    isStable = false;
            }

            foreach(Cell cell in Cells.ToList().Where(c => !c.Alive && HowManyNeighboursAreAlive(c) == 3))
            {
                    cell.TempStateDuringNextStepCalculation = true;
                    isStable = false;
            }

            Cells.ToList().ForEach(c => c.Alive = c.TempStateDuringNextStepCalculation);
            UpdateStateAndStatus(isStable, ++GenerationNumber);
        }

        /// <summary>
        /// Update The State and Status.
        /// </summary>
        /// <param name="isStable"></param>
        private void UpdateStateAndStatus(bool isStable, int GenerationNumber)
        {
            PopulationCount = HowManyAreAlive();

            if(GenerationNumber >= MaxGenerationNumber)
            {
                State = STATE.PAUSE;
                Status = STATUS.MAX_GENERATION;
            }
            else if(PopulationCount == 0)
            {
                State = STATE.PAUSE;
                Status = STATUS.EXTINCT;
            }
            else if(isStable)
            {
                State = STATE.PAUSE;
                Status = STATUS.STABLE;
            }
            else
            {
                Status = STATUS.RUNNING;
            }
        }

        private int HowManyNeighboursAreAlive(Cell cell)
        {
            CellToNeighboursMapping.TryGetValue(cell, out var neighbours);
            return neighbours.Where(c => c.Alive).Count();
        }

        private int HowManyAreAlive()
        {
            return Cells.Where(c => c.Alive).Count();
        }

        private static volatile bool m_sound = true;
        public bool Sound
        {
            get
            {
                return m_sound;
            }
            set
            {
                if (m_sound != value)
                {
                    m_sound = value;
                    if (m_sound)
                    {
                        PlayTheme();
                    }
                    else
                    {
                        StopTheme();
                    }
                    NotifyPropertyChanged();
                }
            }
        }

        public void PlayTheme()
        {
            if (Sound)
            {
                Task.Run(() =>
                {
                    SoundManager.PlayTheme();
                });
            }
        }

        private static void StopTheme()
        {
            Task.Run(() =>
            {
                SoundManager.StopTheme();
            });
        }

        private ICommand _about = null;
        public ICommand AboutCmd
        {
            get
            {
                if (_about == null)
                {
                    _about = new RelayCommand(
                        p => About(),
                        p => ReturnTrue());
                }
                return _about;
            }
        }


        private static void About()
        {
            new About().Show();
        }
        

        private static bool ReturnTrue()
        {
            return true;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}