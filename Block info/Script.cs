void Main()
{
    IMyTextPanel lcd = (IMyTextPanel) GridTerminalSystem.GetBlockWithName("LCD Panel");
    IMyTerminalBlock vent = (IMyTerminalBlock) GridTerminalSystem.GetBlockWithName("Airlock Air Vent");
    //IMyTerminalBlock vent = (IMyTerminalBlock) GridTerminalSystem.GetBlockWithName("Battery 4");

    List<ITerminalProperty> props = new List<ITerminalProperty>();    
    vent.GetProperties(props);
    lcd.WritePublicText(vent.CustomName + " - propetry list - " + props.Count, false);
    for(int i=0; i<props.Count; i=i+1){
        lcd.WritePublicText("\r\n" + props[i], true);
    }

    List<ITerminalAction> actions = new List<ITerminalAction>();
    vent.GetActions(actions);    
    lcd.WritePublicText("\r\n\r\n" + vent.CustomName + " - action list - " + actions.Count, true); 
    for(int i=0; i<actions.Count; i=i+1){ 
        lcd.WritePublicText("\r\n" + actions[i].Id, true); 
    } 
}