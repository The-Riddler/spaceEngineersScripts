//
//Config
//
static int period = 10; //Time between runs in seconds
static int maxIngotDiff = 1000000; //1KG (SE Amount.RawValue comparison [milligrams])

//
//Code
//
static IMyTextPanel display;
static int ingotsLastVal = -1; //-1 is not a valid value for this (can not use null)
static double avg1min = 0;
static double avg5min = 0;
static double avg15min = 0;
static int runs = 0;

void Main()  
{
    //Update display block every run and clear it - just incase we change the block
    IMyTextPanel display = (IMyTextPanel) GridTerminalSystem.GetBlockWithName("LCD");
    display.WritePublicText("-----[[Station power monitoring, run:" + runs++ + "]]----- ", false);

    List<IMyTerminalBlock> reactors = new List<IMyTerminalBlock>();  
    GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors); 
    List<IMyTerminalBlock> batteries = new List<IMyTerminalBlock>();    
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);   
    int ingotsKG = 0;
    int reactorPowerAvailable = 0;  
    int batteryPowerAvailable = 0;  
    int extraReactorPower = 0; 
   
    //display.WritePublicText("Checking reactors", false);
    for(int i=0; i<reactors.Count; ++i){ 
        int oldIngots = ingotsKG;  
        IMyReactor r = (IMyReactor) reactors[i];  
        List<IMyInventoryItem> items = r.GetInventory(0).GetItems();            
    
        for(int j=0; j<items.Count; ++j){
            IMyInventoryItem currentItem = items[j]; 
            //display.WritePublicText("\r\nChecking items " + j + " > " + currentItem.Amount.RawValue, true);
            ingotsKG = ingotsKG + (int) currentItem.Amount.RawValue;       
        }

        //display.WritePublicText("\r\nChecking ingots " + ingotsKG, true);
        //If reactor has ingots, it can produce power  
        if(oldIngots < ingotsKG){
            //display.WritePublicText("\r\nAdded", true);
            int output = getMaxBlockOutput(r);  
            reactorPowerAvailable += output;  
            extraReactorPower += (output - getCurrentBlockOutput(r));  
        }  
    }

    int diff = ingotsLastVal - ingotsKG;
    if(ingotsLastVal >= 0 && ingotsLastVal >= ingotsKG && diff < maxIngotDiff) doHistory(display, diff);
    display.WritePublicText("\n\rIngots: " + massToFriendly(ingotsKG) + " d" + massToFriendly(diff), true);
    ingotsLastVal = ingotsKG;
    display.WritePublicText("\n\rAvg time: "
        + secondsToFriendly((int) (ingotsKG/avg1min*period))
        + "/" + secondsToFriendly((int) (ingotsKG/avg5min*period))
        + "/" + secondsToFriendly((int) (ingotsKG/avg15min*period)),
        true
    );

    display.WritePublicText("\n\rAvail: " + reactorPowerAvailable, true);
    display.WritePublicText("\n\rExtra: " + extraReactorPower, true);   
    display.WritePublicText("\n\rChecking batteries", true);
    for(int i=0; i<batteries.Count; ++i){  
        extraReactorPower += checkBatteryState(extraReactorPower, (IMyBatteryBlock) batteries[i]);  
    } 
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

    if((tmp = seconds / 86400.0) > 1) return String.Format("{0:0.00}d", tmp);
    if((tmp = seconds / 3600.0) > 1) return String.Format("{0:0.00}h", tmp);
    if((tmp = seconds / 60.0) > 1) return String.Format("{0:0.00}m", tmp);
    return seconds + "s";
}

void doHistory(IMyTextPanel display, int ingotsDiff)
{
    double x = (double) ingotsDiff;        
    double exp1 = 1.0/Math.Exp(period/60.0);
    double exp5 = 1.0/Math.Exp(period/300.0);
    double exp15 = 1.0/Math.Exp(period/900.0);
    avg1min = avg1min * exp1 + x * (1-exp1);
    avg5min = avg5min * exp5 + x * (1-exp5);
    avg15min = avg15min * exp15 + x * (1-exp15);
    /*display.WritePublicText("\n\rHistory "
        + String.Format("{0:0.00}", exp1)
        + "/" + String.Format("{0:0.00}", exp5)    
        + "/" + String.Format("{0:0.00}", exp15),
        true
    );*/
    display.WritePublicText("\n\rAvg:"
        + String.Format("{0:0.00}", avg1min)
        + "/" + String.Format("{0:0.00}", avg5min)
        + "/" + String.Format("{0:0.00}", avg15min),
        true
    );
}

bool isBatteryRecharging(IMyBatteryBlock b)  
{          
    return b.DetailedInfo.Contains("Fully recharged in");      
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
    
int checkBatteryState(int extraReactorPower, IMyBatteryBlock b) 
{ 
    int maxOutput = getMaxBlockOutput(b);  
    int maxInput = getMaxBlockInput(b);  
    int currentOutput = getCurrentBlockOutput(b);      
    bool charging = isBatteryRecharging(b);
    bool notRequired = extraReactorPower > currentOutput;
    bool canCharge = extraReactorPower > maxInput;
    bool powerLow = extraReactorPower <= 0;

    //Power low => discharge what we can onto the grid
    //Without knowing what percentage below power we are (not possible at the moment)
    //    it is not safe to assume we just added extra power, so this will switch
    //    all batteries on then turn some off if we over reacted
    if(powerLow){ setCharge(b, false); setOn(b, true); return 0;}

    //Can charge => stop whatever we were doing and charge
    if(canCharge){ setCharge(b, true); setOn(b, true); return -maxInput;}

    //Not needed but we can't charge => disable
    if(notRequired){ setOn(b, false); return -currentOutput;}

    return 0; //No change
}