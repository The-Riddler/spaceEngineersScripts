static int runs = 0;
static int period = 5;
static List<IMyTextPanel> displays;
static string displayTag = "o2Status";
static string state = "READY";
static double lastOxygenVal = 0;
static double avg1min = 0.00005; //Start assuming 1 persons usage of 1 tank
static double avg1minExp = 1.0/Math.Exp(5.0/60.0);
static double maxDiff = 0.001; //More than people should ever use

void Main()
{
    setupDisplays();

    printOxygenStats();
    checkAirVents();
    printState();
}

void printState()
{
    write("\r\n===State===\r\n" + state + "\r\n", true);
}

void checkAirVents()
{
    write("\r\n===Air vents===\r\n", true); 
    List<IMyTerminalBlock> vents = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.GetBlocksOfType<IMyAirVent>(vents); 
 
    bool closeDoors = false; 
    for(int i=0; i<vents.Count; i=i+1){ 
        IMyAirVent vent = (IMyAirVent) vents[i]; 
        if(vent.CustomName.Contains("[airlock]")){ 
             write(String.Format("{0}: Airlock {1:P}\r\n",
                vent.CustomName.Replace("[airlock]", "").Trim(),
                vent.GetOxygenLevel()
             ), true);
            continue;
        } 
 
        if(vent.IsPressurized() != true){ 
            if(vent.Enabled){ 
                closeDoors = true; 
                write(vent.CustomName + ": FAULT\r\n", true); 
            }else{     
                write(vent.CustomName + ": Disabled\r\n", true); 
            } 
        }else{
            write(String.Format("{0}: {1:P}\r\n", vent.CustomName, vent.GetOxygenLevel()), true);
        } 
    } 
 
    if(closeDoors && state == "READY"){
        state = "ACTIVE";
        List<IMyTerminalBlock> doors = new List<IMyTerminalBlock>(); 
        GridTerminalSystem.GetBlocksOfType<IMyDoor>(doors); 
 
        for(int i=0; i<doors.Count; i=i+1){ 
            doors[i].GetActionWithName("Open_Off").Apply(doors[i]); 
        } 
    }else if(!closeDoors){
        state = "READY";
    }
}

void printOxygenStats()
{
    List<IMyTerminalBlock> tanks = new List<IMyTerminalBlock>(); 
    GridTerminalSystem.GetBlocksOfType<IMyOxygenTank>(tanks);

    double totalOxygen = 0;
    write("===Oxygen tanks===\r\n", true);
    for(int i=0; i<tanks.Count; i=i+1){
        IMyOxygenTank tank = (IMyOxygenTank) tanks[i];
        double level = tank.GetOxygenLevel();
        totalOxygen += level;
        write(String.Format("{0}: {1:P}\r\n", tank.CustomName, level), true);
    }

    if(lastOxygenVal > totalOxygen){ //Going down
        double diff = lastOxygenVal - totalOxygen;
        write("Oxygen diff: " + diff + "\r\n", true);  
        if(diff < maxDiff) avg1min = avg1min * avg1minExp + diff * (1-avg1minExp); 
        //write("Avg: " + avg1min + "\r\n", true);
        write("    Oxygen level: depleted in " + secondsToFriendly((int) (totalOxygen/avg1min*period)) + "\r\n", true);
    }

    if(lastOxygenVal == totalOxygen){
        write("    Oxygen level: stable\r\n", true);
    }else if(lastOxygenVal < totalOxygen){
        write("    Oxygen level: rising\r\n", true);
    }

    lastOxygenVal = totalOxygen;
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
            display.WritePublicText("-----[[Station oxygen monitoring, run:" + runs + "]]-----\r\n", false); 
            displays.Add(display); 
        } 
    } 
} 
 
void write(string s, bool overwrite) 
{ 
    for(int i=0; i<displays.Count; ++i) displays[i].WritePublicText(s, overwrite); 
}

string secondsToFriendly(int seconds) 
{ 
    double tmp; 
 
    if((tmp = seconds / 86400.0) > 1) return String.Format("{0:0}d", tmp); 
    if((tmp = seconds / 3600.0) > 1) return String.Format("{0:0.00}h", tmp); 
    if((tmp = seconds / 60.0) > 1) return String.Format("{0:0.00}m", tmp); 
    return seconds + "s"; 
}
