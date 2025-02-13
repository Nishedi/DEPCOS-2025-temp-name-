﻿using Gurobi;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GurobiVRP


{
    public List<List<int>> subSets = new List<List<int>>();
    private static GRBEnv env ;
    public GurobiVRP()
    {

        env = new GRBEnv();
        env.Set(GRB.IntParam.OutputFlag, 0);
    }

    // Function that generates all possible combinations of customers
    public void GetCombination(List<Customer> customers)
    {
        List<int> list = new List<int>();
        foreach (var loc in customers)
        {
            if (loc.Id != 0)
                list.Add(loc.Id);
        }
        double count = Math.Pow(2, list.Count);
        for (int i = 1; i <= count - 1; i++)
        {
            string str = Convert.ToString(i, 2).PadLeft(list.Count, '0');
            List<int> subSet = new List<int>();
            for (int j = 0; j < str.Length; j++)
            {
                if (str[j] == '1')
                {
                    subSet.Add(list[j]);
                }
            }
            if (subSet.Count > 1)
                subSets.Add(subSet);
        }
    }

    public void printSubSets()
    {
        foreach (var subSet in subSets)
        {
            foreach (var item in subSet)
            {
                Console.Write(item + " ");
            }
            Console.WriteLine();
        }
    }
    public Tuple<double, double> gurobi_test(VRPTW problem)
    {
        // Generate all possible combinations
        GetCombination(problem.Customers);
        // Create a new model
        GRBModel model = new GRBModel(env);
        // Set time limit
        double minutes = 10;
        model.Set(GRB.DoubleParam.TimeLimit, minutes * 60);
        int locationsNumber = problem.Customers.Count;
        GRBVar[, ,] x = new GRBVar[problem.NumberOfVehicles, locationsNumber, locationsNumber];
        GRBVar[,] y = new GRBVar[problem.NumberOfVehicles, locationsNumber];
        GRBVar[,] t = new GRBVar[problem.NumberOfVehicles, locationsNumber];
        //GRBVar[,] b = new GRBVar[problem.NumberOfVehicles, locationsNumber];
        //GRBVar[,] d = new GRBVar[problem.NumberOfVehicles, locationsNumber];
        GRBVar[,] penB = new GRBVar[problem.NumberOfVehicles, locationsNumber];
        GRBVar[,] penD = new GRBVar[problem.NumberOfVehicles, locationsNumber];
        GRBVar[,] wait = new GRBVar[problem.NumberOfVehicles, locationsNumber];

        // Create variable x[v, i, j]
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for(int i = 0; i < locationsNumber; i++)
            {
                for (int j = 0; j < locationsNumber; j++)
                {
                    x[v, i, j] = model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, "x_" + v + "_" + i + "_" + j);
                }
            }
        }

        // Create variable y[v, i]
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 1; i < locationsNumber; i++)
            {
                y[v, i] = model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, "y_" + v + "_" + i);

            }
        }

        // Create variable t[v, i]
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                //t[v, i] = model.AddVar(0.0, 10000.0, 0.0, GRB.INTEGER, "t_" + v + "_" + i);
                t[v, i] = model.AddVar(0.0, problem.Customers[0].dv, 0.0, GRB.INTEGER, "t_" + v + "_" + i); 
            }
        }

        /* DEPRECATED - upperbounds introduced in penB and penD
        // Create variable b[v, i] - time of arrival before time window (penalty)
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                b[v, i] = model.AddVar(0.0, 10000.0, 0.0, GRB.INTEGER, "b_" + v + "_" + i);
            }
        }

        // Create variable d[v, i] - time of arrival after time window (penalty)
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                d[v, i] = model.AddVar(0.0, 10000.0, 0.0, GRB.INTEGER, "d_" + v + "_" + i);
            }
        }
        */

        // Create variable penB[v, i] - penalty for arrival before time window (penalty)
        // Limited by service time
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                //penB[v, i] = model.AddVar(0.0, 10000.0, 0.0, GRB.INTEGER, "penB_" + v + "_" + i);
                penB[v, i] = model.AddVar(0.0, problem.Customers[i].ServiceTime, 0.0, GRB.INTEGER, "penB_" + v + "_" + i);

            }
        }

        // Create variable penD[v, i] - penalty for arrival after time window (penalty)
        // Limited by service time
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                //penD[v, i] = model.AddVar(0.0, 10000.0, 0.0, GRB.INTEGER, "penD_" + v + "_" + i);
                penD[v, i] = model.AddVar(0.0, problem.Customers[i].ServiceTime, 0.0, GRB.INTEGER, "penD_" + v + "_" + i);
            }
        }

        // Create variable wait[v, i] - wyjazd po czasie
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                wait[v, i] = model.AddVar(0.0, 10000.0, 0.0, GRB.INTEGER, "wait_" + v + "_" + i);
            }
        }


        model.Update();

        // Set objective
        GRBLinExpr expr = 0.0;
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                for (int j = 0; j < locationsNumber; j++)
                {
                    expr += x[v, i, j] * problem.distanceMatrix[i,j];
                }
            }
            //Add service time
            for (int i = 0; i < locationsNumber; i++)
            {
                expr += y[v, i] * problem.Customers[i].ServiceTime;
                expr += problem.Customers[i].penalty * penB[v, i];
                expr += problem.Customers[i].penalty * penD[v, i];
                expr += wait[v, i];
            }
        }
        model.SetObjective(expr, GRB.MINIMIZE);

        // Add constraint 2
        // Time constraint
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            GRBLinExpr sum = 0.0;
            // Add travelling time
            for (int i = 0; i < locationsNumber; i++)
            {
                for (int j = 0; j < locationsNumber; j++)
                {
                    sum += x[v, i, j] * problem.distanceMatrix[i, j];
                }
            }
            //Add service time
            for (int i = 0; i < locationsNumber; i++)
            {
                sum += y[v, i] * problem.Customers[i].ServiceTime;
                sum += problem.Customers[i].penalty * penB[v, i];
                sum += problem.Customers[i].penalty * penD[v, i];
                sum += wait[v, i];
            }  
            // Checking if the time is less than the vehicle working time. Vehicle 0 is take, because fleet is homogeneous
            model.AddConstr(sum, GRB.LESS_EQUAL, problem.Vehicles[0].wv, "c2");
        }

        // Add constraint 3 
        // A location must be assigned to exactly one vehicle
        for (int i = 1; i < locationsNumber; i++)
        {
            GRBLinExpr sum = 0.0;
            for (int v = 0; v < problem.NumberOfVehicles; v++)
                {
                sum += y[v, i];
            }
            model.AddConstr(sum, GRB.EQUAL, 1.0, "c3");
        }

        // Add constraint 4
        // Setting x variables depending on y variables
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 1; i < locationsNumber; i++)
            {
                GRBLinExpr sum = 0.0;
                for (int j = 0; j < locationsNumber; j++)
                {
                    sum += x[v, j, i];
                }
                // Less equal jesli jedna lokalizacja moze byc odwiedzona przez wiecej niz jedno auto
                model.AddConstr(y[v,i], GRB.EQUAL, sum, "c4"); 
            }
        }

        // Add constraint 5
        // Vehicle must start from depot
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 1; i < locationsNumber; i++)
            {
                GRBLinExpr sum = 0.0;
                for (int j = 0; j < locationsNumber; j++)
                {
                    sum += x[v, 0, j];
                }
                model.AddConstr(y[v, i], GRB.LESS_EQUAL, sum, "c5");
            }

        }

        // Add constraint 6
        // A vehicle must leve location only once
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            GRBLinExpr sum = 0.0;
            for (int i = 0; i < locationsNumber; i++)
            {
                sum += x[v, i, 0];
            }
            model.AddConstr(sum, GRB.LESS_EQUAL, 1.0, "c6");
        }

        // Add constraint 7
        // A vehicle must not go to the same location
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            GRBLinExpr sum = 0.0;
            for (int i = 0; i < locationsNumber; i++)
            {
                sum += x[v, i, i];
            }
                model.AddConstr(sum, GRB.EQUAL, 0.0, "c7");
        }

        // Add constraint 8
        // A vehicle must enter and leave a location
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                GRBLinExpr sumEnter = 0.0;
                GRBLinExpr sumLeave = 0.0;
                for (int j = 0; j < locationsNumber; j++)
                {
                    sumEnter += x[v, i, j];
                    sumLeave += x[v, j, i];
                }
                model.AddConstr(sumEnter, GRB.EQUAL, sumLeave, "c8");
            }
        }

        // Add constraint 9
        // No subtour constraint
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            foreach (var subSet in subSets)
            {
                GRBLinExpr sum = 0.0;
                foreach (var i in subSet)
                {
                    foreach (var j in subSet)
                    {
                        if (i != j)
                            sum += x[v, i, j];
                    }
                }
                model.AddConstr(sum, GRB.LESS_EQUAL, subSet.Count - 1, "c9");
            }
        }

        // Add constraint 10
        // Set arrival time variables
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                for(int j = 1; j < locationsNumber; j++)
                {
                    GRBLinExpr sum = t[v, i] + problem.distanceMatrix[i, j] + problem.Customers[i].ServiceTime - 10000*(1 - x[v, i, j]);
                    model.AddConstr(sum, GRB.LESS_EQUAL, t[v, j], "c10");
                }
            }
        }

        /*
        // Add constraint 10 and 11
        // Set time variables range
       
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                model.AddConstr(t[v, i], GRB.GREATER_EQUAL, 0.0, "c10");
            }
        }

        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                model.AddConstr(t[v, i], GRB.LESS_EQUAL, problem.Customers[0].dv, "c11");
            }
        }
        

        // Add constraint 11
        // b and penB must be greater than 0
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                //model.AddConstr(b[v, i], GRB.GREATER_EQUAL, 0.0, "c12");
                model.AddConstr(penB[v, i], GRB.GREATER_EQUAL, 0.0, "c13");
            }
        }

        // Add constraint 14 and 15
        // d and penD must be greater than 0
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                //model.AddConstr(d[v, i], GRB.GREATER_EQUAL, 0.0, "c14");
                //model.AddConstr(penD[v, i], GRB.GREATER_EQUAL, 0.0, "c15");
                model.AddConstr(penD[v, i], GRB.GREATER_EQUAL, 0.0, "c14");
            }
        }
        */

        // Add constraint 11
        // Calculation of b variable and its penatly penB
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 1; i < locationsNumber; i++)
            {
                GRBLinExpr sum = problem.Customers[i].bv - t[v, i] - 10000*(1 - y[v, i]);
                //model.AddConstr(b[v, i], GRB.GREATER_EQUAL, sum, "c16");
                //model.AddGenConstrMin(penB[v, i], new GRBVar[] {b[v, i]}, problem.Customers[i].ServiceTime, "c17");
                model.AddConstr(penB[v, i], GRB.GREATER_EQUAL, sum, "c11");

            }
        }

        // Add constraint 12
        // Calculation of d variable and its penatly penD
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 1; i < locationsNumber; i++)
            {
                GRBLinExpr sum = t[v, i] + problem.Customers[i].ServiceTime - problem.Customers[i].dv - 10000 * (1 - y[v, i]);
                //model.AddConstr(d[v, i], GRB.GREATER_EQUAL, sum, "c18");
                //model.AddGenConstrMin(penD[v, i], new GRBVar[] {d[v, i]}, problem.Customers[i].ServiceTime, "c19");
                model.AddConstr(penD[v, i], GRB.GREATER_EQUAL, sum, "c12");
            }
        }

        // Add constraint 13
        // Calculation of d variable and its penatly penD
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                for (int j = 0; j < locationsNumber; j++)
                {
                    GRBLinExpr sum = t[v, j] - problem.Customers[i].ServiceTime - problem.distanceMatrix[i, j] - t[v, i] - 10000 * (1 - x[v, i, j]);
                    model.AddConstr(wait[v, i], GRB.GREATER_EQUAL, sum, "c13");
                }
            }
        }



        // Optimize model
        model.Optimize();

        // Print model variables - to comment for experiments
        GRBVar[] vars = model.GetVars();
        /*foreach (var v in vars)
            Console.WriteLine(v.VarName + " = " + v.X);*/

        // Goal function value and operation time in seconds
        double fCelu = model.ObjVal;
        double operationTime = model.Runtime;
        //model.Write("model.lp");
        // Dispose (Clean) of model and environment
        model.Dispose();
        env.Dispose();
        return new Tuple<double, double>(fCelu, operationTime);
    }   
}
