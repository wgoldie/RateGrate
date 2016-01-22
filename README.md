# RateGrate
A library for intelligent rate limit handling, based on a new design of how clients should interpret and handle API rate limits.

Currently in development, there is no implementation code yet. 
In fact the project structure itself is heavily subject to change at this point.

The project consists of:
- The RateGrade library itself. 
  This will probably target a .NET dll, and will more broadly serve as a implementation example for the RateGrate "design pattern".
- A TestApiServer project, which will be used to simulate a generic or potentially emulate real apis (twitter, facebook, etc)
  for testing purposes.
- A TestClient project, which will serve as an example project integrating the RateGrate library,
  and will include and/or consist of unit tests for the library's functions, 
  with the goal of 100% test and example coverage (hopefully these will be the same thing).
  
please contrib
