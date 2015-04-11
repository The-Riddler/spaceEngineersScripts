//
//Config
//
static int period = 5; //Time between runs in seconds
static int maxIngotDiff = 1000000; //1KG (SE Amount.RawValue comparison [milligrams])
static string displayTag = "[pwrStatus]";

//
//Code
//
static List<IMyTextPanel> displays;
static int ingotsLastVal = -1; //-1 is not a valid value for this (can not use null)
static int ingotsCurrentVal = 0;
static int maxReactorPower;
static int extraReactorPower;
static double avg1min = 1;
static double avg5min = 1;
static double avg15min = 1;
static bool powerLow = false;
static int runs = 0;

void Main()  
{
    //Update display block every run and clear it - just incase we change the block(s)
    setupDisplays();

    write("===Reactors===\r\n", true);
    updateReactorStats();
    printReactorStats();

    write("\r\n===Power stats===\r\n", true);
    int diff = ingotsLastVal - ingotsCurrentVal;
    if(ingotsLastVal >= 0 && ingotsLastVal > ingotsCurrentVal && diff < maxIngotDiff) doHistory(diff);
    printHistory();  

    write("\r\n===Batteries===\r\n", true);
    manageBatteries();
}

void updateReactorStats()
{
    List<IMyTerminalBlock> reactors = new List<IMyTerminalBlock>();   
    GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors);   

    //Reset old reactor stats
    maxReactorPower = 0; 
    extraReactorPower = 0;  
    ingotsLastVal = ingotsCurrentVal;
    ingotsCurrentVal = 0;
 
    for(int i=0; i<reactors.Count; ++i){  
        int oldIngots = ingotsCurrentVal;   
        IMyReactor r = (IMyReactor) reactors[i];   
        List<IMyInventoryItem> items = r.GetInventory(0).GetItems();             
        if(!r.Enabled) continue;
        for(int j=0; j<items.Count; ++j){ 
            IMyInventoryItem currentItem = items[j];
            ingotsCurrentVal += (int) currentItem.Amount.RawValue;       
        } 

        //If reactor has ingots, it can produce power   
        if(oldIngots < ingotsCurrentVal){
            int output = getMaxBlockOutput(r);   
            maxReactorPower += output;   
            extraReactorPower += (output - getCurrentBlockOutput(r));   
        }   
    }
}

void printReactorStats()
{
    write("Ingots: " + massToFriendly(ingotsCurrentVal) + "\r\n", true);
    write(String.Format(
        "Power: {0:n0} / {1:n0} = {2:p}\r\n",
        extraReactorPower, maxReactorPower, 1-((float) extraReactorPower/maxReactorPower)
    ), true);
}

void setupDisplays()
{
    runs = runs + 1;
    displays = new List<IMyTextPanel>();
    List<IMyTerminalBlock> tmp = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(tmp);
    for(int i=0; i<tmp.Count; ++i){
        IMyTextPanel display = (IMyTextPanel) tmp[i];
        if(display.CustomName.Contains(displayTag)){
            display.WritePublicText("-----[[Station power monitoring, run:" + runs + "]]-----\r\n", false);
            displays.Add(display);
        }
    }
}

void write(string s, bool overwrite)
{
    for(int i=0; i<displays.Count; ++i) displays[i].WritePublicText(s, overwrite);
}

string massToFriendly(int mass, bool raw = true)
{
    double tmp;
    
    if(raw) mass = mass/1000; //Longs stored by Amount.RawValue seem to be milligrams

    if((tmp = mass / 1000000.0) > 1) return String.Format("{0:0.00}t", tmp); 
    if((tmp = mass / 1000.0) > 1) return String.Format("{0:0.00}kg", tmp); 
    return mass + "g";
}

string secondsToFriendly(int seconds)
{
    double tmp;

    if((tmp = seconds / 86400.0) > 1) return String.Format("{0:0}d", tmp);
    if((tmp = seconds / 3600.0) > 1) return String.Format("{0:0.00}h", tmp);
    if((tmp = seconds / 60.0) > 1) return String.Format("{0:0.00}m", tmp);
    return seconds + "s";
}

void doHistory(int ingotsDiff)
{
    double x = (double) ingotsDiff;
    double exp1 = 1.0/Math.Exp(period/60.0);
    double exp5 = 1.0/Math.Exp(period/300.0);
    double exp15 = 1.0/Math.Exp(period/900.0);
    avg1min = Math.Max(1, avg1min * exp1 + x * (1-exp1));
    avg5min = Math.Max(1, avg5min * exp5 + x * (1-exp5));
    avg15min = Math.Max(1, avg15min * exp15 + x * (1-exp15));
}

void printHistory()
{
    int diff = Math.Max(1, ingotsLastVal-ingotsCurrentVal);
    //string format = "{0:0}"; //"{0:0.00}";
    write(String.Format(
        "History: {0} | {1:0} / {2:0} / {3:0}\r\n",
        diff, avg1min, avg5min, avg15min
    ), true); 
    write(String.Format( 
        "Time: {0} | {1} / {2} / {3}\r\n", 
        secondsToFriendly(ingotsCurrentVal/diff*period), 
        secondsToFriendly((int) (ingotsCurrentVal/avg1min*period)),
        secondsToFriendly((int) (ingotsCurrentVal/avg5min*period)),
        secondsToFriendly((int) (ingotsCurrentVal/avg15min*period))
    ), true);
    /*
    write("\n\rAvg time: " 
        + secondsToFriendly((int) (ingotsCurrentVal/avg1min*period)) 
        + "/" + secondsToFriendly((int) (ingotsCurrentVal/avg5min*period)) 
        + "/" + secondsToFriendly((int) (ingotsCurrentVal/avg15min*period)), 
        true 
    );
    write("\n\rHistory: " + ingotsDiff + " | " 
        + String.Format(format, avg1min) 
        + "/" + String.Format(format, avg5min) 
        + "/" + String.Format(format, avg15min), 
        true 
    );
    */
}

bool isBatteryRecharging(IMyBatteryBlock b)  
{          
    return b.DetailedInfo.Contains("Fully recharged in");      
}  

bool needsCharged(IMyBatteryBlock b)   
{           
    return !b.DetailedInfo.Contains("Fully recharged in: 0 sec");       
} 

int getBlockEnergyFromInfo(IMyTerminalBlock b, String prop)
{
    System.Text.RegularExpressions.MatchCollection m = System.Text.RegularExpressions.Regex.Matches(   
        b.DetailedInfo,   
        prop + ": ([0-9]+.?[0-9]*) (MW|kW|W)"   
    );
    if(m.Count == 0) throw new Exception("Error getting prop [" + prop + "] no matches in: \n\r" + b.DetailedInfo); 
    double val = Convert.ToDouble(m[0].Groups[1].Value);    
    String unit = m[0].Groups[2].Value;
    switch(unit){
    case "MW": val = val * 1000000; break;
    case "kW": val = val * 1000; break;
    case "W": break;
    default:
        throw new Exception("Unknown energy unit " + unit);
    }

    return (int) Math.Round(val);    
} 


int getMaxBlockInput(IMyTerminalBlock b)  
{      
    return getBlockEnergyFromInfo(b, "Max Required Input");
} 
 
int getMaxBlockOutput(IMyTerminalBlock b) 
{ 
    return getBlockEnergyFromInfo(b, "Max Output");
} 
 
int getCurrentBlockOutput(IMyTerminalBlock b)  
{  
    return getBlockEnergyFromInfo(b, "Current Output"); 
} 

void setCharge(IMyBatteryBlock b, bool state)
{
    if(isBatteryRecharging(b) != state) b.GetActionWithName("Recharge").Apply(b);
}

void setOn(IMyFunctionalBlock b, bool state)
{
    if(b.Enabled != state) b.GetActionWithName("OnOff").Apply(b);
}

void manageBatteries()
{
    List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    
    int timeleft = (int)(ingotsCurrentVal/avg1min*period);
    bool powerLow = timeleft < 900 || extraReactorPower <= 0;
    bool canCharge = timeleft > 7200;
    
    //Discharge if required
    for(int i=0; i<batteries.Count; ++i){
        IMyBatteryBlock b = (IMyBatteryBlock) batteries[i];
        int maxInput = getMaxBlockInput(b);
        int currentOutput = getCurrentBlockOutput(b);
        bool charging = isBatteryRecharging(b);
 
       if(powerLow){ //Low, turn on all batteries
            setCharge(b, false);
            setOn(b, true);
        }else{
            if(canCharge && extraReactorPower > maxInput && needsCharged(b)){
                extraReactorPower -= maxInput;
                canCharge = false;
                setOn(b, true); setCharge(b, true);
            }else if(!charging && extraReactorPower > currentOutput){ //Not low anymore, turn off what we can
                extraReactorPower -= currentOutput;
                setOn(b, false);
            }
        }

        printBatteryState(b);
    } 
}

void printBatteryState(IMyBatteryBlock b)
{
    bool charging = isBatteryRecharging(b);
    if(b.Enabled){
        if(charging){
            if(needsCharged(b)){
                write(b.CustomName + ": Charging\r\n", true);
            }else{
                write(b.CustomName + ": Idle \r\n", true);
            }
        }else{
            write(b.CustomName + ": Discharging\r\n", true);
        }
    }else{
        write(b.CustomName + ": Idle \r\n", true);
    }
}