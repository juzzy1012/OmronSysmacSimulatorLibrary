(* ============================================================================
   TestVariables.ST - Structured Text for OmronSysmacSimulator Integration Tests
   
   IMPORTANT: Variables must be PUBLISHED to be accessible via NexSocket!
   
   Instructions:
   1. Create a new Sysmac Studio project
   2. Add Type definitions to Data Types (or use structured text in a POU)
   3. Add Variables to Global Variables
   4. **CRITICAL**: For EACH variable, right-click -> Properties -> check "Publish"
      OR select all variables -> right-click -> Publish Variable(s)
   5. Build the project (Ctrl+F7)
   6. Start the simulator (Ctrl+F5 or Online -> Simulation Mode)
   7. Run the C# integration tests
   
   Alternative: You can verify published variables by going to:
   Tools -> Export Global Variables -> CX-Designer
   This will copy published variables to the clipboard.
   
   Note: The "ERROR=0105 Invalid parameter" error usually means the variable
   is not published or doesn't exist in the running project.
   ============================================================================ *)

(* ============================================================================
   TYPE DEFINITIONS
   Add these to your project's Data Types or as Structured Data Types
   ============================================================================ *)

TYPE TestSimpleStruct_t :
    STRUCT
        Id : DINT;
        Temperature : REAL;
        IsActive : BOOL;
    END_STRUCT;
END_TYPE

TYPE TestNestedStruct_t :
    STRUCT
        OuterId : DINT;
        Inner : TestSimpleStruct_t;
        OuterFlag : BOOL;
    END_STRUCT;
END_TYPE

TYPE TestMachineState_t : INT (
    Idle := 0,
    Running := 1,
    Paused := 2,
    Faulted := 3
);
END_TYPE

TYPE TestStructWithEnum_t :
    STRUCT
        Id : DINT;
        State : TestMachineState_t;
    END_STRUCT;
END_TYPE

TYPE TestStructWithArray_t :
    STRUCT
        Count : DINT;
        Values : ARRAY[0..4] OF REAL;
    END_STRUCT;
END_TYPE

TYPE TestLargeStruct_t :
    STRUCT
        Header : DINT;
        Data : ARRAY[0..99] OF REAL;
        Footer : DINT;
    END_STRUCT;
END_TYPE

(* ============================================================================
   GLOBAL VARIABLES
   Add these to your project's Global Variables section
   Make sure to set Publish = True (or appropriate network publish setting)
   ============================================================================ *)

VAR_GLOBAL
    (* Primitive Test Variables - Read Only *)
    TestBoolTrue : BOOL := TRUE;
    TestBoolFalse : BOOL := FALSE;
    TestInt : INT := 12345;
    TestDInt : DINT := 123456789;
    TestReal : REAL := 3.14159;
    TestLReal : LREAL := 2.718281828;
    TestSInt : SINT := -42;
    TestUSInt : USINT := 200;
    TestUInt : UINT := 50000;
    TestUDInt : UDINT := 3000000000;
    TestLInt : LINT := -9223372036854775800;
    TestULInt : ULINT := 18446744073709551000;
    
    (* Primitive Test Variables - Read/Write *)
    TestWriteBool : BOOL := FALSE;
    TestWriteInt : INT := 0;
    TestWriteReal : REAL := 0.0;
    TestWriteDInt : DINT := 0;
    
    (* Array Test Variables *)
    TestIntArray : ARRAY[0..4] OF INT := [100, 200, 300, 400, 500];
    TestRealArray : ARRAY[0..2] OF REAL := [1.1, 2.2, 3.3];
    
    (* Structured Type Test Variables *)
    TestSimpleStructVar : TestSimpleStruct_t;
    TestNestedStructVar : TestNestedStruct_t;
    TestEnumStructVar : TestStructWithEnum_t;
    TestArrayStructVar : TestStructWithArray_t;
    TestLargeStructVar : TestLargeStruct_t;
    
    (* Initialization flag *)
    TestVarsInitialized : BOOL := FALSE;
END_VAR

(* ============================================================================
   INITIALIZATION PROGRAM
   Add this code to your main Program or create a new Program called "TestInit"
   This will run once to set up the test values
   ============================================================================ *)

PROGRAM TestInitProgram
VAR
    i : INT;
END_VAR

(* Only initialize once *)
IF NOT TestVarsInitialized THEN
    
    (* Initialize TestSimpleStructVar *)
    TestSimpleStructVar.Id := 42;
    TestSimpleStructVar.Temperature := 98.6;
    TestSimpleStructVar.IsActive := TRUE;
    
    (* Initialize TestNestedStructVar *)
    TestNestedStructVar.OuterId := 1;
    TestNestedStructVar.Inner.Id := 100;
    TestNestedStructVar.Inner.Temperature := 50.5;
    TestNestedStructVar.Inner.IsActive := TRUE;
    TestNestedStructVar.OuterFlag := TRUE;
    
    (* Initialize TestEnumStructVar *)
    TestEnumStructVar.Id := 1;
    TestEnumStructVar.State := TestMachineState_t#Running;  (* = 1 *)
    
    (* Initialize TestArrayStructVar *)
    TestArrayStructVar.Count := 5;
    TestArrayStructVar.Values[0] := 10.0;
    TestArrayStructVar.Values[1] := 20.0;
    TestArrayStructVar.Values[2] := 30.0;
    TestArrayStructVar.Values[3] := 40.0;
    TestArrayStructVar.Values[4] := 50.0;
    
    (* Initialize TestLargeStructVar *)
    TestLargeStructVar.Header := 12345;
    TestLargeStructVar.Footer := 54321;
    FOR i := 0 TO 99 DO
        TestLargeStructVar.Data[i] := INT_TO_REAL(i) + 100.0;
    END_FOR;
    
    TestVarsInitialized := TRUE;
    
END_IF;

END_PROGRAM
