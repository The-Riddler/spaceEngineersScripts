//
//Config
//
static int period = 5; //Time between runs in seconds
static string displayTag = "[refStatus]"; 
static Dictionary<string, int> lastValues = new Dictionary<string, int>();
static Dictionary<string, double> rates = new Dictionary<string, double>();
static int maxChange = 1000; //More than this wont be taken into the rates calculation

//
//Code
//
static List<IMyTextPanel> displays;

static int runs = 0;

void Main()  
{
    //Update display block every run and clear it - just incase we change the block(s)
    setupDisplays();

    List<IMyTerminalBlock> refinaries = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refinaries);
    for(int i=0; i<refinaries.Count; i++){
        checkRefinery((IMyRefinery) refinaries[i]);
    }
}

void checkRefinery(IMyRefinery r)
{
    if(r.Enabled != true){ write(String.Format("{0}: Disabled\r\n", r.CustomName), true); return;}

    List<IMyInventoryItem> items = r.GetInventory(0).GetItems();
    if(items.Count <= 0){ write(String.Format("{0}: Idle    \r\n", r.CustomName), true); return;}
 
    int amount = (int) items[0].Amount; 
    double rate = 1;
    string key = r.CustomName + ":" + r.NumberInGrid;
    if(lastValues.ContainsKey(key)){    
        int diff = lastValues[key] - amount;
        string ratesKey = key + ":" + items[0].Content.SubtypeName;
        if(rates.ContainsKey(ratesKey) && diff < maxChange){
            double exp = 1/Math.Exp(period/300.0);
            rates[ratesKey] = rates[ratesKey] * exp + diff * (1-exp);
        }else{
            rates[ratesKey] = diff;
        }
        rate = rates[ratesKey];
    }
    lastValues[key] = amount;

    write(String.Format(
        "{0}: {1} - {2} - {3} - {4}/s\r\n",
        r.CustomName,
        items[0].Content.SubtypeName,
        massToFriendly((int) items[0].Amount),
        secondsToFriendly((int) (amount/rate*period)),
        massToFriendly((int) rate)
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
            display.WritePublicText("-----[[Refinery monitoring, run:" + runs + "]]-----\r\n", false);
            displays.Add(display);
        }
    }
}

void write(string s, bool overwrite)
{
    for(int i=0; i<displays.Count; ++i) displays[i].WritePublicText(s, overwrite);
}

string massToFriendly(int mass)
{
    double tmp;

    if((tmp = mass / 1000000.0) > 1) return String.Format("{0:0.00}t", tmp);
    if((tmp = mass / 1000.0) > 1) return String.Format("{0:0.00}kg", tmp);

    return mass + "g";
}

string secondsToFriendly(int seconds)
{
    double tmp;

    if((tmp = seconds / 86400.0) > 1) return String.Format("{0:0}d", tmp);
    if((tmp = seconds / 3600.0) > 1) return String.Format("{0:0}h", tmp);
    if((tmp = seconds / 60.0) > 1) return String.Format("{0:0}m", tmp);
    return seconds + "s";
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

void setOn(IMyFunctionalBlock b, bool state)
{
    if(b.Enabled != state) b.GetActionWithName("OnOff").Apply(b);
}